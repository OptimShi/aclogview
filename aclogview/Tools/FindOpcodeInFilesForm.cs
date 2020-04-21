using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

using aclogview.Properties;
using aclogview.Tools.Parsers;
using static aclogview.Form1;

namespace aclogview.Tools
{
    public partial class FindOpcodeInFilesForm : Form
    {
        public FindOpcodeInFilesForm()
        {
            InitializeComponent();
        }

        // key is [Name of the speaker]+","+MessageText+","+[ChatMessageType of text]
        // val is the log filename
        private Dictionary<string, string> Speech = new Dictionary<string, string>();

        // contains a more full record than "Speech", including wcid, timestampes, etc, but Speech is still used to track if the item has been found already.
        private Dictionary<string, string> SpeechDump = new Dictionary<string, string>();

        private string logFileName = "D:\\Source\\Speech-" + DateTime.Today.ToString("yyyy-MM-dd") + ".csv";

        private void ResetLogFile()
        {
            using (StreamWriter theFile = new StreamWriter(logFileName, false))
                theFile.WriteLine("WCID,Time,Name,Text,Type,Log");
        }

        private void SaveResultsToLogFile()
        {
            using (StreamWriter theFile = new StreamWriter(logFileName, true))
            {
                foreach (KeyValuePair<string, string> entry in SpeechDump)
                {
                    theFile.Write(entry.Key + ",");
                    theFile.Write(entry.Value);
                    theFile.WriteLine();
                }
            }
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);

            txtSearchPathRoot.Text = Settings.Default.FindOpcodeInFilesRoot;
            txtOpcode.Text = Settings.Default.FindOpcodeInFilesOpcode.ToString("X4");

            typeof(DataGridView).InvokeMember("DoubleBuffered", BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.SetProperty, null, dataGridView1, new object[] { true });
            dataGridView1.RowHeadersVisible = false;
            dataGridView1.AllowUserToAddRows = false;
            dataGridView1.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            dataGridView1.Columns[0].ValueType = typeof(int);
            dataGridView1.Columns[1].ValueType = typeof(int);

            // Center to our owner, if we have one
            if (Owner != null)
                Location = new Point(Owner.Location.X + Owner.Width / 2 - Width / 2, Owner.Location.Y + Owner.Height / 2 - Height / 2);
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            searchAborted = true;

            Settings.Default.FindOpcodeInFilesRoot = txtSearchPathRoot.Text;
            Settings.Default.FindOpcodeInFilesOpcode = OpCode;

            base.OnClosing(e);
        }


        private void btnChangeSearchPathRoot_Click(object sender, EventArgs e)
        {
            using (FolderBrowserDialog openFolder = new FolderBrowserDialog())
            {
                if (openFolder.ShowDialog() == DialogResult.OK)
                    txtSearchPathRoot.Text = openFolder.SelectedPath;
            }
        }

        private void txtOpcode_KeyPress(object sender, KeyPressEventArgs e)
        {
            if (e.KeyChar == (char)Keys.Enter)
            {
                e.Handled = true;
                btnStartSearch.PerformClick();
            }
        }

        int OpCode
        {
            get
            {
                int.TryParse(txtOpcode.Text, NumberStyles.HexNumber, null, out var value);

                return value;
            }
        }

        private void dataGridView1_CellDoubleClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex == -1)
                return;

            var fileName = (string)dataGridView1.Rows[e.RowIndex].Cells[3].Value;

            System.Diagnostics.Process.Start(Application.ExecutablePath, "-f" + '"' + fileName + '"' + " -o " + opCodeToSearchFor);
        }


        private readonly OpcodeFinder parser = new OpcodeFinder();

        private List<string> filesToProcess = new List<string>();

        private int opCodeToSearchFor;

        private int filesProcessed;
        private int totalHits;
        private int totalExceptions;
        private bool searchAborted;

        private readonly ConcurrentBag<OpcodeFinderResult> processFileResults = new ConcurrentBag<OpcodeFinderResult>();

        private void btnStartSearch_Click(object sender, EventArgs e)
        {
            Stopwatch watch = new Stopwatch();
            watch.Start();

            dataGridView1.RowCount = 0;
            richTextBox1.Clear();

            try
            {
                btnStartSearch.Enabled = false;

                filesToProcess = ToolUtil.GetPcapsInFolder(txtSearchPathRoot.Text);

                opCodeToSearchFor = OpCode;

                filesProcessed = 0;
                totalHits = 0;
                totalExceptions = 0;
                searchAborted = false;

                while (!processFileResults.IsEmpty)
                    processFileResults.TryTake(out _);

                toolStripStatusLabel1.Text = "Files Processed: 0 of " + filesToProcess.Count;

                txtSearchPathRoot.Enabled = false;
                txtOpcode.Enabled = false;
                btnChangeSearchPathRoot.Enabled = false;
                btnStopSearch.Enabled = true;

                // Clear the log file from any previous searches...
                ResetLogFile();

                // Do the actual search here
                DoSearch();

                // Save results to the log file
                SaveResultsToLogFile();

                watch.Stop();
                string watchTimerText = watch.Elapsed.TotalSeconds.ToString();
                TimeSpan ts = watch.Elapsed;
                // Format and display the TimeSpan value.
                string elapsedTime = String.Format("{0:00}:{1:00}:{2:00}.{3:00}",
                    ts.Hours, ts.Minutes, ts.Seconds,
                    ts.Milliseconds / 10);
                MessageBox.Show("Log File Processing Complete.\n\n" + elapsedTime + "");
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString());

                btnStopSearch_Click(null, null);
            }
        }

        private void btnStopSearch_Click(object sender, EventArgs e)
        {
            searchAborted = true;

            txtSearchPathRoot.Enabled = true;
            txtOpcode.Enabled = true;
            btnChangeSearchPathRoot.Enabled = true;
            btnStartSearch.Enabled = true;
            btnStopSearch.Enabled = false;
        }


        private void DoSearch()
        {
            int progress = 0;
            //filesToProcess.Clear(); filesToProcess.Add("d:\\Asheron's Call\\Log Files\\PCAP Part 1\\Julianna_pcap\\pkt_2017-1-30_1485830024_log.pcap");
            foreach (string currentFile in filesToProcess)
            {
                if (searchAborted || Disposing || IsDisposed)
                    return;

                progress++;
                LogProgress(progress, filesToProcess.Count, currentFile);

                try
                {
                    ProcessFile(currentFile);
                }
                catch { }
            }
        }

        private void LogProgress(int progress, int total, string filename)
        {
            using (StreamWriter theFile = new StreamWriter("D:\\Source\\aclogview_progress.txt", false))
            {
                //Calculate percentage earlier in code
                decimal percentage = (decimal)progress / total;
                theFile.WriteLine(progress.ToString() + " of " + total.ToString() + " - " + percentage.ToString("0.00%"));
                theFile.WriteLine(filename);
                theFile.WriteLine("Speech Entries: " + Speech.Count.ToString());
            }
        }

        private class CreatedItem
        {
            public uint Wcid;
            public string Name;

            public CreatedItem(uint wcid, string name)
            {
                Wcid = wcid;
                Name = name;
            }
        }

        private bool isPcapng;
        private double startTimer;
        private string currentTimer;

        private void ProcessFile(string fileName)
        {
            int hits = 0;
            int exceptions = 0;
            resetTimers();
            //var result = parser.ProcessFileRecords(currentFile, records, ref searchAborted, opCodeToSearchFor);
            var records = PCapReader.LoadPcap(fileName, true, ref searchAborted, out isPcapng);

            Dictionary<uint, CreatedItem> items = new Dictionary<uint, CreatedItem>();

            string myPath = "D:\\Asheron's Call\\Log Files\\";
            string logFilenameVal = fileName.Replace(myPath, "");

            uint trackingGuid = 0;
            var eventsWithoutHit = 0;

            string quot = "\"";

            foreach (PacketRecord record in records)
            {

                // ********************************************************************
                // ************************ CUSTOM SEARCH CODE ************************ 
                // ********************************************************************
                // Custom search code that can output information to Special Output
                // Below are several commented out examples on how you can search through bulk pcaps for targeted data, and output detailed information to the output tab.

                try
                {
                    if (record.data.Length <= 4)
                        continue;

                    BinaryReader messageDataReader = new BinaryReader(new MemoryStream(record.data));

                    PacketOpcode opcode = Util.readOpcode(messageDataReader);
                    //////////
                    switch (opcode)
                    {
                        case PacketOpcode.CHARACTER_ENTER_GAME_EVENT: // Reset/clear all of these references.
                            items.Clear();
                            resetTimers();
                            eventsWithoutHit = 0;
                            break;

                        case PacketOpcode.Evt_Physics__CreateObject_ID: // Stores the guid => wcid/name of all created items so we can reference it
                            eventsWithoutHit = 0;
                            var createMsg = CM_Physics.CreateObject.read(messageDataReader);
                            uint guid = createMsg.object_id;
                            uint wcid = createMsg.wdesc._wcid;
                            CreatedItem item = new CreatedItem(wcid, createMsg.wdesc._name.m_buffer);
                            if (items.ContainsKey(guid))
                                items[guid] = item;
                            else
                                items.Add(guid, item);

                            break;
                        case PacketOpcode.Evt_Physics__DeleteObject_ID: // Remove the item from our CreatedItems list...
                            eventsWithoutHit = 0;
                            var deleteMsg = CM_Physics.DeleteObject.read(messageDataReader);
                            uint delGuid = deleteMsg.object_id;
                            if (items.ContainsKey(delGuid))
                                items.Remove(delGuid);
                            break;
                        case PacketOpcode.Evt_Communication__HearDirectSpeech_ID: // 0x02BD
                            eventsWithoutHit = 0;
                            var hearDirectSpeechMsg = CM_Communication.HearDirectSpeech.read(messageDataReader);
                            // This will filter out players...
                            if (IsNotPlayer(hearDirectSpeechMsg.SenderID))
                            {
                                eChatTypes chatType = (eChatTypes)hearDirectSpeechMsg.ChatMessageType;
                                string key = "\"" + hearDirectSpeechMsg.SenderName.m_buffer.Replace(quot, quot + quot) + "\",\"" + hearDirectSpeechMsg.MessageText.m_buffer.Replace(quot, quot + quot) + "\"," + chatType.ToString();
                                if (!Speech.ContainsKey(key))
                                {
                                    setTimers(record);
                                    Speech.Add(key, logFilenameVal);

                                    uint hearDirectSpeechWCID = 0;
                                    if (items.ContainsKey(hearDirectSpeechMsg.SenderID))
                                        hearDirectSpeechWCID = items[hearDirectSpeechMsg.SenderID].Wcid;
                                    key = hearDirectSpeechWCID.ToString() + "," + currentTimer + "," + key;
                                    SpeechDump.Add(key, logFilenameVal);
                                }
                            }
                            break;
                        case PacketOpcode.Evt_Communication__HearSpeech_ID: // 02BB
                            eventsWithoutHit = 0;
                            var hearSpeechMsg = CM_Communication.HearSpeech.read(messageDataReader);
                            // This will filter out players...
                            if (IsNotPlayer(hearSpeechMsg.SenderID))
                            {
                                eChatTypes chatType = (eChatTypes)hearSpeechMsg.ChatMessageType;
                                string key = "\"" + hearSpeechMsg.SenderName.m_buffer.Replace(quot, quot+quot) + "\",\"" + hearSpeechMsg.MessageText.m_buffer.Replace(quot, quot + quot) + "\"," + chatType.ToString();
                                if (!Speech.ContainsKey(key))
                                {
                                    setTimers(record);
                                    Speech.Add(key, logFilenameVal);

                                    uint myWCID = 0;
                                    if (items.ContainsKey(hearSpeechMsg.SenderID))
                                        myWCID = items[hearSpeechMsg.SenderID].Wcid;
                                    key = myWCID.ToString() + "," + currentTimer + "," + key;
                                    SpeechDump.Add(key, logFilenameVal);
                                }
                            }
                            break;
                        case PacketOpcode.Evt_Communication__HearRangedSpeech_ID: // 02BC
                            eventsWithoutHit = 0;
                            var hearRangedMsg = CM_Communication.HearRangedSpeech.read(messageDataReader);
                            // This will filter out players...
                            if (IsNotPlayer(hearRangedMsg.SenderID))
                            {
                                eChatTypes chatType = (eChatTypes)hearRangedMsg.ChatMessageType;
                                string key = "\"" + hearRangedMsg.SenderName.m_buffer.Replace("\"", "\"\"") + "\",\"" + hearRangedMsg.MessageText.m_buffer.Replace("\"", "\"\"") + "\"," + chatType.ToString();
                                if (!Speech.ContainsKey(key))
                                {
                                    setTimers(record);
                                    Speech.Add(key, logFilenameVal);
                                    uint myWCID = 0;
                                    if (items.ContainsKey(hearRangedMsg.SenderID))
                                        myWCID = items[hearRangedMsg.SenderID].Wcid;
                                    key = myWCID.ToString() + "," + currentTimer + "," + key;
                                    SpeechDump.Add(key, logFilenameVal);
                                }
                            }
                            break;
                        case PacketOpcode.Evt_Communication__HearEmote_ID: // 02BC
                            eventsWithoutHit = 0;
                            var hearEmoteMsg = CM_Communication.HearEmote.read(messageDataReader);
                            // This will filter out players...
                            if (IsNotPlayer(hearEmoteMsg.SenderID))
                            {
                                string chatType = "Emote";
                                string key = "\"" + hearEmoteMsg.SenderName.m_buffer.Replace("\"", "\"\"") + "\",\"" + hearEmoteMsg.EmoteMessage.m_buffer.Replace("\"", "\"\"") + "\"," + chatType;
                                if (!Speech.ContainsKey(key))
                                {
                                    setTimers(record);
                                    Speech.Add(key, logFilenameVal);
                                    uint myWCID = 0;
                                    if (items.ContainsKey(hearEmoteMsg.SenderID))
                                        myWCID = items[hearEmoteMsg.SenderID].Wcid;
                                    key = myWCID.ToString() + "," + currentTimer + "," + key;
                                    SpeechDump.Add(key, logFilenameVal);
                                }
                            }
                            break;
                        case PacketOpcode.Evt_Communication__HearSoulEmote_ID:
                            eventsWithoutHit = 0;
                            var heatSoulEmoteMsg = CM_Communication.HearSoulEmote.read(messageDataReader);
                            // This will filter out players...
                            if (IsNotPlayer(heatSoulEmoteMsg.SenderID))
                            {
                                string chatType = "SoulEmote";
                                string key = "\"" + heatSoulEmoteMsg.SenderName.m_buffer.Replace("\"", "\"\"") + "\",\"" + heatSoulEmoteMsg.EmoteMessage.m_buffer.Replace("\"", "\"\"") + "\"," + chatType;
                                if (!Speech.ContainsKey(key))
                                {
                                    Speech.Add(key, logFilenameVal);

                                    setTimers(record);
                                    uint myWCID = 0;
                                    if (items.ContainsKey(heatSoulEmoteMsg.SenderID))
                                        myWCID = items[heatSoulEmoteMsg.SenderID].Wcid;
                                    key = myWCID.ToString() + "," + currentTimer + "," + key;
                                    SpeechDump.Add(key, logFilenameVal);
                                }
                            }
                            break;
                        case PacketOpcode.Evt_Inventory__GiveObjectRequest_ID:
                            var useGiveObjectMsg = CM_Inventory.GiveObjectRequest.read(messageDataReader);
                            setTimers(record);
                            trackingGuid = useGiveObjectMsg.i_targetID;
                            break;
                        case PacketOpcode.Evt_Inventory__UseEvent_ID:
                            var useEventMsg = CM_Inventory.UseEvent.read(messageDataReader);
                            setTimers(record);
                            trackingGuid = useEventMsg.i_object;
                            break;
                        case PacketOpcode.Evt_Movement__MovementEvent_ID:
                            if (trackingGuid > 0)
                            {
                                eventsWithoutHit = 0;
                                var movementEventMsg = CM_Movement.MovementEvent.read(messageDataReader);
                                if (trackingGuid == movementEventMsg.object_id)
                                {
                                    string movement = "";

                                    // has an "Interpreted Motion"
                                    switch (movementEventMsg.movement_data.movementData_Unpack.movement_type)
                                    {
                                        case MovementTypes.Type.Invalid:
                                            if ((movementEventMsg.movement_data.movementData_Unpack.interpretedMotionState.bitfield & 2) != 0) // (uint)PackBitfield.forward_command
                                                movement = movementEventMsg.movement_data.movementData_Unpack.style.ToString() + " - " + movementEventMsg.movement_data.movementData_Unpack.interpretedMotionState.forward_command.ToString();
                                            break;
                                        case MovementTypes.Type.MoveToObject:
                                        case MovementTypes.Type.MoveToPosition:
                                        case MovementTypes.Type.TurnToHeading:
                                        case MovementTypes.Type.TurnToObject:
                                            movement = movementEventMsg.movement_data.movementData_Unpack.style.ToString() + " - " + movementEventMsg.movement_data.movementData_Unpack.movement_type.ToString();
                                            break;

                                    }

                                    if (movement != "" && movement != "Motion_NonCombat - Motion_Off" && movement != "Motion_NonCombat - Motion_On")
                                    {
                                        string chatType = "MovementEvent";

                                        uint movementWCID = 0;
                                        if (items.ContainsKey(movementEventMsg.object_id))
                                        {
                                            movementWCID = items[movementEventMsg.object_id].Wcid;
                                            if (movementWCID != 1 && movementWCID != 21)
                                            {
                                                // Don't need to look up if we've already said this...
                                                setTimers(record);
                                                string movementName = items[movementEventMsg.object_id].Name.Replace("\"", "\"\"");
                                                string key = "\"" + movementName + "\",\"" + movement + "\"," + chatType;
                                                uint myWCID = movementWCID;
                                                key = myWCID.ToString() + "," + currentTimer + "," + key;
                                                SpeechDump.Add(key, logFilenameVal);
                                            }

                                        }
                                    }
                                }
                            }
                            break;
                        default:
                            eventsWithoutHit++;
                            if (eventsWithoutHit > 30)
                            {
                                resetTimers();
                                trackingGuid = 0;
                            }
                            break;
                    }

                }
                catch
                {
                    // Do something with the exception maybe
                    exceptions++;

                    Interlocked.Increment(ref totalExceptions);
                }
            }

            Interlocked.Increment(ref filesProcessed);

            items.Clear();

            //processFileResults.Add(new ProcessFileResult() { FileName = fileName, Hits = hits, Exceptions = exceptions });
        }

        private void resetTimers()
        {
            startTimer = 0;
            currentTimer = "0";
        }

        private void setTimers(PacketRecord record)
        {
            if (startTimer == 0)
            {
                startTimer = Convert.ToDouble(getTimestamp(record));
                currentTimer = "0";
            }
            else
            {
                double myTimestamp = Convert.ToDouble(getTimestamp(record));
                currentTimer = string.Format("{0:0.##}", myTimestamp - startTimer);
            }
        }

        private string getTimestamp(PacketRecord record)
        {
            if (isPcapng)
            {
                long microseconds = record.tsHigh;
                microseconds = (microseconds << 32) | record.tsLow;

                if (Settings.Default.PacketsListviewTimeFormat == (byte)TimeFormat.LocalTime)
                    return Utility.EpochTimeToLocalTime(microseconds);

                var time = $"{microseconds / (decimal) 1000000:F6}";
                return time;
            }
            else
            {
                if (Settings.Default.PacketsListviewTimeFormat == (byte)TimeFormat.LocalTime)
                    return Utility.EpochTimeToLocalTime(record.tsSec, record.tsUsec);

                return $"{record.tsSec}." + $"{record.tsUsec:D6}";
            }
        }

        private bool IsNotPlayer(uint guid)
        {
            if (guid > 0x50000000 && guid < 0x50FFFFFF)
                return false;

            return true;
        }

    }
}
