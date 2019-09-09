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
        private CancellationTokenSource cancellationTokenSource;

        public void Start()
        {
            Log.Write(LogType.Info, "Starting QueueWatcher");
            networkWorker = new Network();
            cancellationTokenSource = new CancellationTokenSource();
            FindFFXIVProcess();
            QueueWatch(cancellationTokenSource.Token);
        }

        private Task<bool> QueueWatch(CancellationToken cancellationToken)
        {
            Task<bool> task = null;
            bool needRunning = true;

            try
            {
                task = Task.Run(() =>
                {
                    try
                    {
                        while (needRunning)
                        {
                            if (cancellationToken.IsCancellationRequested)
                            {
                                Log.Write(LogType.Debug, "Cancellation Requested");
                            }

                            if (!cancellationToken.IsCancellationRequested)
                            {
                                Thread.Sleep(10 * 1000);

                                if (FFXIVProcess == null || FFXIVProcess.HasExited)
                                {
                                    FindFFXIVProcess();
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
                                        networkWorker.StartCapture(FFXIVProcess, cancellationToken);
                                    }
                                }
                            }
                            else
                            {
                                needRunning = false;
                                Log.Write(LogType.Info, "Stopping QueueWatcher");
                                throw new TaskCanceledException(task);
                            }
                        }
                    }
                    catch (TaskCanceledException taskCanceledException)
                    {
                        Log.Write(LogType.Info, "Task canceled", taskCanceledException);
                    }

                    return task;
                });
            }
            catch (TaskCanceledException taskCanceledException)
            {
                Log.Write(LogType.Info, "Task canceled", taskCanceledException);
            }



            return task;
        }

        public void Stop()
        {
            Log.Write(LogType.Info, "Trying to stop QueueWatcher");

            if (networkWorker != null)
            {
                networkWorker.StopCapture();
            }

            cancellationTokenSource.Cancel();
            cancellationTokenSource.Dispose();  
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
        }


    }
}
