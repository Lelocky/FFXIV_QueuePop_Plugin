//ORIGINAL SOURCE: https://github.com/devunt/DFAssist
using FFXIV_QueuePop_Plugin.Logger;
using FFXIV_QueuePop_Plugin.Notifier;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;

namespace FFXIV_QueuePop_Plugin
{
    internal partial class Network
    {
        private State state = State.IDLE;
        private int lastMember = 0;
        
        private void AnalyseFFXIVPacket(byte[] payload)
        {
            try {
                while (true)
                {
                    if (payload.Length < 4)
                    {
                        break;
                    }

                    var type = BitConverter.ToUInt16(payload, 0);

                    if (type == 0x0000 || type == 0x5252)
                    {
                        if (payload.Length < 28)
                        {
                            break;
                        }

                        var length = BitConverter.ToInt32(payload, 24);

                        if (length <= 0 || payload.Length < length)
                        {
                            break;
                        }

                        using (var messages = new MemoryStream(payload.Length))
                        {
                            using (var stream = new MemoryStream(payload, 0, length))
                            {
                                stream.Seek(40, SeekOrigin.Begin);

                                if (payload[33] == 0x00)
                                {
                                    stream.CopyTo(messages);
                                }
                                else {
                                    stream.Seek(2, SeekOrigin.Current);

                                    using (var z = new DeflateStream(stream, CompressionMode.Decompress))
                                    {
                                        z.CopyTo(messages);
                                    }
                                }
                            }
                            messages.Seek(0, SeekOrigin.Begin);

                            var messageCount = BitConverter.ToUInt16(payload, 30);
                            for (var i = 0; i < messageCount; i++)
                            {
                                try
                                {
                                    var buffer = new byte[4];
                                    var read = messages.Read(buffer, 0, 4);
                                    if (read < 4)
                                    {
                                        Log.Write(LogType.Error, "l-analyze-error-length");
                                        break;
                                    }
                                    var messageLength = BitConverter.ToInt32(buffer, 0);

                                    var message = new byte[messageLength];
                                    messages.Seek(-4, SeekOrigin.Current);
                                    messages.Read(message, 0, messageLength);

                                    HandleMessage(message);
                                }
                                catch (Exception ex)
                                {
                                    Log.Write(LogType.Error, "l-analyze-error-general1", ex);
                                }
                            }
                        }

                        if (length < payload.Length)
                        {
                            payload = payload.Skip(length).ToArray();
                            continue;
                        }
                    }
                    else
                    {
                        for (var offset = 0; offset < payload.Length - 2; offset++)
                        {
                            var possibleType = BitConverter.ToUInt16(payload, offset);
                            if (possibleType == 0x5252)
                            {
                                payload = payload.Skip(offset).ToArray();
                                AnalyseFFXIVPacket(payload);
                                break;
                            }
                        }
                    }

                    break;
                }
            }
            catch (Exception ex)
            {
                Log.Write(LogType.Error, "l-analyze-error", ex);
            }
        }

        private void HandleMessage(byte[] message)
        {
            try
            {
                if (message.Length < 32)
                {
                    return;
                }
                var opcode = BitConverter.ToUInt16(message, 18);

#if !DEBUG
                if (opcode != 0x0078 &&
                    opcode != 0x0079 &&
                    opcode != 0x0080 &&
                    opcode != 0x006C &&
                    opcode != 0x006F &&
                    opcode != 0x0121 &&
                    opcode != 0x0143 &&
                    opcode != 0x022F)
                    return;
#endif

                var data = message.Skip(32).ToArray();

                if (opcode == 0x022F)
                {
                    var code = BitConverter.ToInt16(data, 4);
                    var type = data[8];


                    if (type == 0x0B)
                    {
                        Log.Write(LogType.Info, "l-field-instance-entered");
                    }
                    else if (type == 0x0C)
                    {
                        Log.Write(LogType.Info, "l-field-instance-left");
                    }

                }
                else if (opcode == 0x0143)
                {
                    var type = data[0];                    
                }
                else if (opcode == 0x0078)
                {
                    var status = data[0];
                    var reason = data[4];

                    if (status == 0)
                    {
                        state = State.QUEUED;

                        var rouletteCode = data[20];

                        if (rouletteCode != 0 && (data[15] == 0 || data[15] == 64))
                        {
                            Log.Write(LogType.Info, "l-queue-started-roulette");
                        }
                        else
                        {
                            Log.Write(LogType.Info, "l-queue-started-general");
                        }
                    }
                    else if (status == 3)
                    {
                        state = reason == 8 ? State.QUEUED : State.IDLE;
                        Log.Write(LogType.Info, "l-queue-stopped");
                    }
                    else if (status == 6)
                    {
                        state = State.IDLE;

                        Log.Write(LogType.Info, "l-queue-entered");
                    }
                    else if (status == 4)
                    {
                        var roulette = data[20];
                        var code = BitConverter.ToUInt16(data, 22);
                        _ = NotificationSender.SendNotification();
                        Log.Write(LogType.Info, "l-queue-matched");
                    }
                }
                else if (opcode == 0x006F)
                {
                    var status = data[0];

                    if (status == 0)
                    {
                      
                    }
                    if (status == 1)
                    {
                        
                    }
                }
                else if (opcode == 0x0121) 
                {
                    var status = data[5];

                    if (status == 128)
                    {
                        
                    }
                }
                else if (opcode == 0x0079)
                {
                    var code = BitConverter.ToUInt16(data, 0);
                    var status = data[4];
                    var tank = data[5];
                    var dps = data[6];
                    var healer = data[7];


                    if (status == 1)
                    {
                        var member = tank * 10000 + dps * 100 + healer;

                        if (state == State.MATCHED && lastMember != member)
                        {                  
                            state = State.QUEUED;
                        }
                        else if (state == State.IDLE)
                        {                          
                            state = State.QUEUED;
                          
                        }
                        else if (state == State.QUEUED)
                        {
                        }

                        lastMember = member;
                    }
                    else if (status == 2)
                    {
                        return;
                    }
                    else if (status == 4)
                    {
                      
                    }
                    Log.Write(LogType.Info, "l-queue-updated");
                }
                else if (opcode == 0x0080)
                {
                    var roulette = data[2];
                    var code = BitConverter.ToUInt16(data, 4);

                    Log.Write(LogType.Info, "l-queue-matched");
                    _ = NotificationSender.SendNotification();
                }
            }
            catch (Exception ex)
            {
                Log.Write(LogType.Error, "l-analyze-error-general", ex);
            }
        }

        private enum State
        {
            IDLE,
            QUEUED,
            MATCHED,
        }
    }
}
