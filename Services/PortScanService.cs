using System.Net;
using System.Data;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.SqlClient;
using portscanner_backend.Data;
using portscanner_backend.Models;
using portscanner_backend.Models.Dto;
using System.Collections.Concurrent;

namespace portscanner_backend.Services
{
    public class PortScanService
    {
        private readonly AppDbContext _context;

        public PortScanService(AppDbContext context)
        {
            _context = context;
        }

        public List<string> ExpandCidr(string cidr)
        {
            var parts = cidr.Split('/');
            var baseIp = IPAddress.Parse(parts[0]);
            int prefix = int.Parse(parts[1]);

            if (prefix == 32) return new List<string> { baseIp.ToString() };

            uint ip = BitConverter.ToUInt32(baseIp.GetAddressBytes().Reverse().ToArray(), 0);
            int hostBits = 32 - prefix;
            uint numberOfHosts = (uint)Math.Pow(2, hostBits);

            var ips = new List<string>();
            uint start = (prefix < 31) ? 1u : 0u; 
            uint end = (prefix < 31) ? numberOfHosts - 1 : numberOfHosts;

            for (uint i = start; i < end; i++)
            {
                uint newIp = ip + i;
                var bytes = BitConverter.GetBytes(newIp).Reverse().ToArray();
                ips.Add(new IPAddress(bytes).ToString());
            }
            return ips;
        }

        public async Task<ConcurrentDictionary<string, bool>> CheckHostsAvailabilityAsync(List<string> ips, int maxConcurrency, int pingTimeout, int pingRetries)
        {
            var hostStatuses = new ConcurrentDictionary<string, bool>();
            
            foreach (var ip in ips) hostStatuses.TryAdd(ip, false);

            var semaphore = new SemaphoreSlim(maxConcurrency);

            var tasks = ips.Select(async ip =>
            {
                await semaphore.WaitAsync();
                try
                {
                    using var ping = new Ping();
                    for (int i = 0; i < pingRetries; i++)
                    {
                        try
                        {
                            var reply = await ping.SendPingAsync(ip, pingTimeout);
                            if (reply.Status == IPStatus.Success)
                            {
                                hostStatuses[ip] = true;
                                break;
                            }
                        }
                        catch { }
                        await Task.Delay(50);
                    }
                }
                finally
                {
                    semaphore.Release();
                }
            });

            await Task.WhenAll(tasks);
            return hostStatuses;
        }

        public async Task<bool> ScanPortAsync(string ip, int port, int timeoutMs)
        {
            try
            {
                using var client = new TcpClient();
                var connectTask = client.ConnectAsync(ip, port);
                var delayTask = Task.Delay(timeoutMs);

                var completed = await Task.WhenAny(connectTask, delayTask);
                if (completed != connectTask) return false;

                await connectTask;
                return client.Connected;
            }
            catch { return false; }
        }

        public async Task<List<IpScanResultDto>> ExecuteSubnetScanAsync(
            string cidr, 
            List<int> targetPorts)
        {
            var config = await _context.AppConfigs.FirstOrDefaultAsync() ?? new AppConfig();
            int concurrency = config.MaxConcurrency;
            int pingTimeout = config.PingTimeout;
            int pingRetries = config.PingRetries;
            int portTimeout = config.PortScanTimeout;

            var ips = ExpandCidr(cidr);

            var hostStatusDict = await CheckHostsAvailabilityAsync(ips, concurrency, pingTimeout, pingRetries);

            var results = new ConcurrentBag<IpScanResultDto>();
            var semaphore = new SemaphoreSlim(concurrency);

            var scanTasks = ips.Select(async ip => 
            {
                await semaphore.WaitAsync();
                try 
                {
                    bool isPingAlive = hostStatusDict.ContainsKey(ip) && hostStatusDict[ip];

                    var ipResult = new IpScanResultDto
                    {
                        Ip = ip,
                        IsHostAlive = isPingAlive,
                        Ports = new List<PortScanResultDto>()
                    };

                    foreach (var port in targetPorts)
                    {
                        bool isOpen = await ScanPortAsync(ip, port, portTimeout);
                        
                        ipResult.Ports.Add(new PortScanResultDto 
                        { 
                            Port = port, 
                            Status = isOpen,
                            Severity = "Info"
                        });
                    }

                    results.Add(ipResult);
                }
                finally 
                {
                    semaphore.Release();
                }
            });

            await Task.WhenAll(scanTasks);

            return results
                .OrderBy(r => Version.TryParse(r.Ip, out var v) ? v.ToString() : r.Ip)
                .ToList();
        }

        public async Task<ScanSession> CreateSessionAsync(string scanTitle, string scanType)
        {
            var session = new ScanSession 
            {
                Title = scanTitle,
                Type = scanType,
                ScanDate = DateTime.UtcNow.AddHours(7) 
            };
            _context.ScanSessions.Add(session);
            await _context.SaveChangesAsync();
            return session;
        }

        public async Task<List<PortStatusChange>> BulkSaveResultsAsync(int branchId, List<IpScanResultDto> results, int sessionId, DateTime scanDate, int? schId = null)
        {
            var portChanges = new List<PortStatusChange>();


            // Ganti lookup "semua port" ke versi longgar yg bisa nembus ke custom ports:
            // Krn 1 Nomor Port bisa ada di banyak group, Dictionary tidak bisa dipakai krn duplikat Key.
            // Gunakan List / Array. Karena sekarang kita menyimpan PortNumber (Bukan Pm_Id).

            var branchIps = await _context.IpAddresses.Where(i => i.Ip_branchId == branchId).ToListAsync();
            var ipDict = branchIps.ToDictionary(i => i.Ip_address, i => i);

            var existingHostPortsList = await _context.HostPorts
                .Include(hp => hp.IpAddress)
                .Where(hp => hp.IpAddress.Ip_branchId == branchId)
                .ToListAsync();
            
            var hostPortDict = existingHostPortsList
                .GroupBy(hp => hp.IpAddress.Ip_address)
                .ToDictionary(g => g.Key, g => g.ToDictionary(hp => hp.Port_number, hp => hp));

            foreach (var ipResult in results)
            {
                if (!ipDict.TryGetValue(ipResult.Ip, out var ipEntity))
                {
                    ipEntity = new IpAddress 
                    {
                        Ip_branchId = branchId,
                        Ip_address = ipResult.Ip,
                        Ip_isAlive = ipResult.IsHostAlive,
                        Ip_lastScanned = scanDate
                    };
                    _context.IpAddresses.Add(ipEntity);
                    ipDict[ipResult.Ip] = ipEntity;
                }
                else
                {
                    ipEntity.Ip_isAlive = ipResult.IsHostAlive;
                    ipEntity.Ip_lastScanned = scanDate;
                }
            }
            await _context.SaveChangesAsync();

            foreach (var ipResult in results)
            {
                var ipEntity = ipDict[ipResult.Ip];
                
                var hostRes = new ScanHostResult
                {
                    Session_id = sessionId,
                    Ip_id = ipEntity.Ip_id,
                    IsAlive = ipResult.IsHostAlive
                };
                _context.ScanHostResults.Add(hostRes);

                hostPortDict.TryGetValue(ipResult.Ip, out var currentHostPortsDict);

                if (ipResult.Ports != null)
                {
                    foreach (var portResult in ipResult.Ports)
                    {
                        var pNum = portResult.Port;
                        
                        if (portResult.Status)
                        {
                            var portRes = new ScanPortResult
                            {
                                ScanHostResult = hostRes, 
                                Port_number = pNum
                            };
                            _context.ScanPortResults.Add(portRes);
                        }

                        if (currentHostPortsDict != null && currentHostPortsDict.TryGetValue(pNum, out var existingHp))
                        {
                            if (existingHp.Status != portResult.Status)
                            {
                                portChanges.Add(new PortStatusChange
                                {
                                    IpAddress = ipResult.Ip,
                                    PortNumber = pNum,
                                    IsNowOpen = portResult.Status
                                });
                            }

                            existingHp.Status = portResult.Status;
                            existingHp.Last_Updated = scanDate;
                        }
                        else
                        {
                            // Jika sebelumnya port ini belum ada sama sekali di database,
                            // maka dianggap sebagai perubahan jika status terbarunya adalah Open (true) atau Closed (false)
                            portChanges.Add(new PortStatusChange
                            {
                                IpAddress = ipResult.Ip,
                                PortNumber = pNum,
                                IsNowOpen = portResult.Status
                            });

                            var newHp = new HostPort
                            {
                                Ip_id = ipEntity.Ip_id,
                                Port_number = pNum,
                                Status = portResult.Status,
                                Last_Updated = scanDate
                            };
                            _context.HostPorts.Add(newHp);
                            if (currentHostPortsDict == null)
                            {
                                currentHostPortsDict = new Dictionary<int, HostPort>();
                                hostPortDict[ipResult.Ip] = currentHostPortsDict;
                            }
                            currentHostPortsDict[pNum] = newHp;
                        }
                    }
                }
            }

            await _context.SaveChangesAsync();

            return portChanges;
        }
    }
}
