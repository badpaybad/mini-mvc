using Newtonsoft.Json;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace MiniMvc.Core.HttpStandard
{
    public class HttpLogger : IDisposable
    {
        static ConcurrentQueue<HttpRequest> _logs = new ConcurrentQueue<HttpRequest>();
        static Thread _threadWriteLog;
        static bool _isStop = false;
        static HttpLogger()
        {
            _threadWriteLog = new Thread(async () => { await LoopWriteLog(); });
            _threadWriteLog.Start();
        }

        private static async Task LoopWriteLog()
        {
            while (!_isStop)
            {
                try
                {
                    List<HttpRequest> logs = new List<HttpRequest>();
                    for (var i = 0; i < 100; i++)
                    {
                        if (_logs.TryDequeue(out HttpRequest info) && info != null)
                        {
                            logs.Add(info);
                        }
                    }

                    //async write your logs by write file or db or redis or elastic search ...
                    var dir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs");
                    if (Directory.Exists(dir) == false) Directory.CreateDirectory(dir);
                    var file = Path.Combine(dir, $"{DateTime.Now.ToString("yyyyMMdd_HH")}.log");

                    var logText = string.Join("\r\n", logs.Select(i => JsonConvert.SerializeObject(i)));

                    using (var sw = new StreamWriter(file, true))
                    {
                        await sw.WriteLineAsync(logText);
                        sw.Flush();
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message);
                }
                finally
                {
                    await Task.Delay(1000);
                }
            }
        }

        public static void Log(HttpRequest request)
        {
            //use queue to async write log            
            _logs.Enqueue(request);
        }

        public void Dispose()
        {
            _isStop = true;
        }
    }

}
