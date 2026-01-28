using System.Net;
using System.Data;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.SqlClient;
using portscanner_backend.Data;
using portscanner_backend.Models;
using portscanner_backend.Models.Dto;

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

            if (prefix == 32)
            {
                return new List<string> { baseIp.ToString() };
            }

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

        public async Task<List<string>> GetAliveHostsAsync(List<string> ips, int maxConcurrency, int pingTimeout, int pingRetries)
        {
            var aliveHosts = new List<string>();
            var semaphore = new SemaphoreSlim(maxConcurrency); 

            var tasks = ips.Select(async ip =>
            {
                await semaphore.WaitAsync();
                try
                {
                    using var ping = new Ping();
                    bool isAlive = false;

                    for (int i = 0; i < pingRetries; i++)
                    {
                        try
                        {
                            var reply = await ping.SendPingAsync(ip, pingTimeout); 
                    
                            if (reply.Status == IPStatus.Success)
                            {
                                lock (aliveHosts)
                                {
                                    isAlive = true;
                                    break;
                                }
                            }
                        }
                        catch
                        {
                            
                        }
                        
                    }
                    if (isAlive)
                    {
                        lock (aliveHosts)
                        {
                            aliveHosts.Add(ip);
                        }
                    }
                }
                catch { }
                finally
                {
                    semaphore.Release();
                }
            });

            await Task.WhenAll(tasks);
            return aliveHosts;
        }

        public async Task<bool> ScanPortAsync(string ip, int port, int timeoutMs)
        {
            try
            {
                using var client = new TcpClient();
                var connectTask = client.ConnectAsync(ip, port);
                var delayTask = Task.Delay(timeoutMs); 

                var completed = await Task.WhenAny(connectTask, delayTask);
                if (completed != connectTask) 
                {
                    return false;
                }
                
                await connectTask; 
                return client.Connected;
            }
            catch
            {
                return false;
            }
        }

        

        public async Task BulkSaveResultsAsync(int branchId, List<IpScanResultDto> results, string scanTitle, string scanType)
        {
            var table = new DataTable();
            table.Columns.Add("BranchId", typeof(int));
            table.Columns.Add("IpAddress", typeof(string));
            table.Columns.Add("PortNumber", typeof(int));
            table.Columns.Add("IsOpen", typeof(bool));
            table.Columns.Add("ScanDate", typeof(DateTime));
            table.Columns.Add("Res_title", typeof(string));
            table.Columns.Add("Res_type", typeof(string));

            var scanDateTemp = DateTime.UtcNow.AddHours(7);
            
            foreach (var ipResult in results)
            {
                if (ipResult.Ports != null) 
                {
                    
                    foreach (var portResult in ipResult.Ports)
                    {
                        table.Rows.Add(
                            branchId, 
                            ipResult.Ip, 
                            portResult.Port, 
                            portResult.Status, 
                            scanDateTemp,
                            scanTitle,
                            scanType
                        );
                    }
                }
            }

            var parameter = new SqlParameter("@ScanData", SqlDbType.Structured)
            {
                TypeName = "dbo.ScanResultType",
                Value = table
            };

            await _context.Database.ExecuteSqlRawAsync("EXEC sp_BulkSaveScanResults @ScanData", parameter);
        }
    }
}
