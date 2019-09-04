using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Advanced_Combat_Tracker;
using FFXIV_QueuePop_Plugin.Logger;

namespace FFXIV_QueuePop_Plugin.Notifier
{
    internal class QueueWatcher
    {
        private Process FFXIVProcess;
        private Network networkWorker;
        private Task networkTask;
    
        public void Start()
        {
            Log.Write(LogType.Info, "Starting QueueWatcher");
            networkWorker = new Network();
            FindFFXIVProcess();
            StartQueueWatcher();   
        }

        private void StartQueueWatcher()
        {
            Task.Factory.StartNew(() =>
            {
                while (true)
                {
                    Thread.Sleep(30 * 1000);

                    if (FFXIVProcess == null || FFXIVProcess.HasExited)
                    {
                        FFXIVProcess = null;
                    }
                    else
                    {
                        // FFXIVProcess is alive

                        if (networkWorker.IsRunning)
                        {
                            networkWorker.UpdateGameConnections(FFXIVProcess);
                        }
                        else
                        {
                            networkWorker.StartCapture(FFXIVProcess, "", "");
                        }
                    }
                }
            });
        }

        public void Stop()
        {
            
        }


        private void FindFFXIVProcess()
        {
            Log.Write(LogType.Info, "l-process-finding");

            var processes = new List<Process>();
            processes.AddRange(Process.GetProcessesByName("ffxiv"));
            processes.AddRange(Process.GetProcessesByName("ffxiv_dx11"));

            if (processes.Count == 0)
            {
                Log.Write(LogType.Info, "l-process-found-nothing");
                
            }
            else if (processes.Count >= 2)
            {
                Log.Write(LogType.Info, "l-process-found-multiple");
            }
            else
            {
                SetFFXIVProcess(processes[0]);
            }
        }

        private void SetFFXIVProcess(Process process)
        {
            FFXIVProcess = process;

            var name = $"{FFXIVProcess.ProcessName}:{FFXIVProcess.Id}";
            Log.Write(LogType.Info, "l-process-set-success " + name);

          
            networkWorker.StartCapture(FFXIVProcess, "", "");
        }


    }
}
