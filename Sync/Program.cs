using Microsoft.Win32;
using Newtonsoft.Json;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Sync
{

    class Program
    {
        class Time : IDisposable
        {
            DateTime start;
            public Time()
            {
                this.start = DateTime.Now;
            }

            public void Dispose()
            {
                var time = DateTime.Now - start;
                Console.WriteLine(time.TotalMilliseconds + "ms");
            }
        }

        class Message
        {
            public string path;
            public string x;
        }
        class Model
        {
            private Stream stream;
            public Model(Stream stream)
            {
                this.stream = stream;
                new Thread(ReaderThread).Start();
            }

            public void FuncCall(int parameter, Action callback)
            {
                callback();
                callback();
            }

            private void ReaderThread()
            {

            }
        }

        static void Main(string[] args)
        {
            /*
            BetterStream stream = new BetterStream();
            string dir = @"C:/subsync/";
            var fileSync = new FileSync(stream);

            fileSync.SyncFiles(dir, new string[] { "*.csproj" });

            return;
            */

            var LocalHost = IPAddress.Parse("127.0.0.1");
            TcpListener listener = new TcpListener(LocalHost, 0);
            listener.Start();
            Task.Run(() =>
            {
                var client = listener.AcceptTcpClient();
                var stream = client.GetStream();
                var serverModel = new CallReader(stream, new Magic());
            });

            int port = ((IPEndPoint)listener.LocalEndpoint).Port;
            {
                TcpClient client = new TcpClient();
                client.Connect(LocalHost, port);

                var stream = client.GetStream();
                var model = DynamicWrapper.CreateClientModel<IMagic>(new CallWriter(stream, typeof(IMagic)));

                var result = model.Test(5, 5342948023980934820L, "test");
                Console.WriteLine(result);
                model.Test(-234, -1L, "test5");

                //var body = model.GetType().GetMethods().First(x => x.Name == "Test").GetMethodBody();

                var result3 = model.TakeList(new List<int>() { 1, 2, 3, 3 });
                Console.WriteLine(string.Join(", ", result3));
                var result2 = model.TakeObj(new ObjectObj() { x = 5 });
                Console.WriteLine(result2.x);

                new Thread(() =>
                {
                    Thread.Sleep(1000);
                    model.Test(5, 5, "asfasdfafsd");
                }).Start();
            }

            //model.Test(test, 1, "str");
            //model.Test(2, 5, "str2");
            

            Console.ReadKey();
            return;

            //string dir = @"\\10.10.84.84\e\scratch\speed\";
            //string dir = @"c:\scratch\speed\";
            string dir = @"C:/Users/quentin.brooks/Dropbox";
            dir = @"C:/";
            /*
            //string dir = @"C:\Users\quentin.brooks\Dropbox\Proxy";

            FindAll(EnumerateRoot(dir, new string[] { "*.csproj" }));

            List<string> files = new List<string>();
            using (new Time())
            {
                EnumerateDirect(dir, files, new string[] { "*.csproj" });
            }
            Console.WriteLine("Found " + files.Count);
            */
            //FindAll(Directory.EnumerateFiles(dir, "*.csproj", SearchOption.AllDirectories));



            //Enumerate(dir);

            /*
            //Directory.GetDirectories()
            Directory.EnumerateDirectories(@"\\10.10.84.84\e\git\public_web_sites\www.zazzle.com\", "*", SearchOption.TopDirectoryOnly).AsParallel()
                .ForAll(file =>
                {
                    Console.WriteLine(file);
                });

            Console.WriteLine("done");
            WatchPath(@"\\10.10.84.84\e\git\public_web_sites\www.zazzle.com", new string[] { "*.ts", "*.tsx" }, change =>
            {
            });
            */

            Console.WriteLine("DONE");
            Console.Read();
        }

        private static void FindAll(IEnumerable<string> enumerator)
        {
            int count = 0;
            using (new Time())
            {
                foreach (string file in enumerator)
                {
                    Console.Write("+");
                    //Console.WriteLine(file);
                    //if (limit-- <= 0) break;
                    count++;
                }
            }
            Console.WriteLine("Found " + count);
            
            
        }




        public static void WatchPath(string path, string[] filters, Action<FileSystemEventArgs> callback)
        {
            List<FileSystemWatcher> watchers = new List<FileSystemWatcher>();
            filters.ToList().ForEach(filter =>
            {
                var watcher = new FileSystemWatcher(path, filter);
                watcher.NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName;
                watcher.IncludeSubdirectories = true;
                watcher.EnableRaisingEvents = true;
                watcher.Deleted += (sender, e) =>
                {

                };
                watcher.Renamed += (sender, e) =>
                {

                };
                watcher.Changed += (sender, e) =>
                {
                    callback(e);
                };
                watchers.Add(watcher);
            });
        }
    }
}
/*
 

            if (!Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
                for (int i = 0; i < count; i++)
                {
                    Directory.CreateDirectory(dir + "folder" + i);
                    for (int k = 0; k < files; k++)
                    {
                        File.WriteAllText(dir + "folder" + i + "/file" + k, i + "-" + k);
                    }
                }
            }

            var obj = new object();

            for (int type = 0; type < 3; type++)
            {
                using (new Time())
                {
                    if (type == 0)
                    {
                        int proof = 0;
                        Directory.GetDirectories(dir, "*").ToList().AsParallel().ForAll(path =>
                        {
                            Directory.GetFiles(path).ToList().ForEach(subPath =>
                            {
                                int result = File.ReadAllText(subPath).Length;
                                lock (obj)
                                {
                                    proof += result;
                                }
                            });
                        });
                        Console.WriteLine(proof);
                    }
                    else if (type == 1)
                    {
                        int proof = 0;
                        Directory.GetDirectories(dir, "*").ForEach(path =>
                        {
                            Directory.GetFiles(path).ForEach(subPath =>
                            {
                                int result = File.ReadAllText(subPath).Length;
                                proof += result;
                            });
                        });
                        Console.WriteLine(proof);
                        //Directory.GetDirectories(dir2, "*").ToList();
                        //Directory.GetDirectories(dir3, "*").ToList();
                    }
                    else if(type == 2)
                    {
                        int proof = 0;
                        Directory.EnumerateDirectories(dir, "*").ToList().ForEach(path =>
                        {
                            for (int k = 0; k < files; k++)
                            {
                                proof += File.ReadAllText(path + "/file" + k).Length;
                            }
                        });
                        Console.WriteLine(proof);
                    }
                }
            }
 */
