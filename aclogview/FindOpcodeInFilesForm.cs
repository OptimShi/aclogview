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

namespace aclogview
{
    public partial class FindOpcodeInFilesForm : Form
    {
        private Dictionary<string, uint> CreatedWCIDs = new Dictionary<string, uint>(); // key is "wcid,name", value is times found
        private Dictionary<string, string> CreatedWCIDsLogs = new Dictionary<string, string>(); // key is "wcid,name", value is first log found in
        private Dictionary<string, uint> AppraisedWCIDs = new Dictionary<string, uint>(); // key is "wcid,name", value is times found
        private Dictionary<string, string> AppraisedWCIDsLogs = new Dictionary<string, string>(); // key is "wcid,name", value is first log found in

        private string logFileName = "D:\\Source\\WCIDs.csv";

        public FindOpcodeInFilesForm()
        {
            InitializeComponent();
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

        int OpCode
        {
            get
            {
                int value;

                int.TryParse(txtOpcode.Text, NumberStyles.HexNumber, null, out value);

                return value;
            }
        }

        private void btnChangeSearchPathRoot_Click(object sender, EventArgs e)
        {
            using (FolderBrowserDialog openFolder = new FolderBrowserDialog())
            {
                if (openFolder.ShowDialog() == DialogResult.OK)
                    txtSearchPathRoot.Text = openFolder.SelectedPath;
            }
        }


        private readonly List<string> filesToProcess = new List<string>();
        private int opCodeToSearchFor;
        private int filesProcessed;
        private int totalHits;
        private int totalExceptions;
        private bool searchAborted;

        private class ProcessFileResut
        {
            public string FileName;
            public int Hits;
            public int Exceptions;
        }

        private readonly ConcurrentBag<ProcessFileResut> processFileResuts = new ConcurrentBag<ProcessFileResut>();
        
        private readonly ConcurrentDictionary<string, int> specialOutputHits = new ConcurrentDictionary<string, int>();
        private readonly ConcurrentQueue<string> specialOutputHitsQueue = new ConcurrentQueue<string>();

        private void btnStartSearch_Click(object sender, EventArgs e)
        {
            dataGridView1.RowCount = 0;

            try
            {
                btnStartSearch.Enabled = false;

                filesToProcess.Clear();
                opCodeToSearchFor = OpCode;
                filesProcessed = 0;
                totalHits = 0;
                totalExceptions = 0;
                searchAborted = false;

                ProcessFileResut result;
                while (!processFileResuts.IsEmpty)
                    processFileResuts.TryTake(out result);


                specialOutputHits.Clear();
                string specialOutputHitsResult;
                while (!specialOutputHitsQueue.IsEmpty)
                    specialOutputHitsQueue.TryDequeue(out specialOutputHitsResult);
                richTextBox1.Clear();


                filesToProcess.AddRange(Directory.GetFiles(txtSearchPathRoot.Text, "*.pcap", SearchOption.AllDirectories));
                filesToProcess.AddRange(Directory.GetFiles(txtSearchPathRoot.Text, "*.pcapng", SearchOption.AllDirectories));

                toolStripStatusLabel1.Text = "Files Processed: 0 of " + filesToProcess.Count;

                txtSearchPathRoot.Enabled = false;
                txtOpcode.Enabled = false;
                btnChangeSearchPathRoot.Enabled = false;
                btnStopSearch.Enabled = true;

                timer1.Start();
                Stopwatch watch = new Stopwatch();
                watch.Start();

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

        private void ResetLogFile()
        {
            using (StreamWriter theFile = new StreamWriter(logFileName, false))
            {
                theFile.WriteLine("WCID,Name,EventType,Hits,LogFile");
            }
        }

        private void SaveResultsToLogFile()
        {
            using (StreamWriter theFile = new StreamWriter(logFileName, true))
            {
                foreach (KeyValuePair<string, uint> entry in CreatedWCIDs)
                {
                    theFile.Write(entry.Key + ",CreateObject," + entry.Value.ToString() + ",");
                    theFile.WriteLine(CreatedWCIDsLogs[entry.Key]);
                }
                foreach (KeyValuePair<string, uint> entry in AppraisedWCIDs)
                {
                    theFile.Write(entry.Key + ",AppraiseInfo," + entry.Value.ToString() + ",");
                    theFile.WriteLine(AppraisedWCIDsLogs[entry.Key]);
                }

            }
        }

        private void btnStopSearch_Click(object sender, EventArgs e)
        {
            searchAborted = true;

            timer1.Stop();

            timer1_Tick(null, null);

            txtSearchPathRoot.Enabled = true;
            txtOpcode.Enabled = true;
            btnChangeSearchPathRoot.Enabled = true;
            btnStartSearch.Enabled = true;
            btnStopSearch.Enabled = false;
        }

        private void LogProgress(int progress, int total)
        {
            using (StreamWriter theFile = new StreamWriter("D:\\Source\\WCID_Progress.txt", false))
            {
                theFile.WriteLine(progress.ToString() + " of " + total.ToString());
            }

        }

        private void DoSearch()
        {
            //Parallel.ForEach(filesToProcess, (currentFile) =>
            int progress = 0;
            foreach(string currentFile in filesToProcess)
            //string currentFile = "D:\\Asheron's Call\\Log Files\\PCAP Part 3\\Miach-Skills-Training-Leveling-Commands-pcap\\Miach-New-Character-Stuff-Misc-pcap\\pkt_2017-1-29_1485727198_log.pcap";
            {
                progress++;
                LogProgress(progress, filesToProcess.Count);
                try
                {
                    ProcessFile(currentFile);
                }
                catch { }
            }
        }

        private void ProcessFile(string fileName)
        {
            int hits = 0;
            int exceptions = 0;

            var records = PCapReader.LoadPcap(fileName, true, ref searchAborted);

            Dictionary<uint, uint> createdObjects = new Dictionary<uint, uint>(); // key is guid, value is wcid
            Dictionary<uint, string> createdObjectNames = new Dictionary<uint, string>(); // key is guid, value is name

            foreach (var record in records)
            {
                // ********************************************************************
                // ************************ CUSTOM SEARCH CODE ************************ 
                // ********************************************************************
                // Custom search code that can output information to Special Output
                // Below are several commented out examples on how you can search through bulk pcaps for targeted data, and output detailed information to the output tab.
                foreach (BlobFrag frag in record.frags)
                {
                    try
                    {
                        if (frag.dat_.Length <= 4)
                            continue;

                        BinaryReader fragDataReader = new BinaryReader(new MemoryStream(frag.dat_));

                        var messageCode = fragDataReader.ReadUInt32();

                        if (messageCode == 0xF745) // Create Object
                        {
                            var parsed = CM_Physics.CreateObject.read(fragDataReader);
                            uint wcid = parsed.wdesc._wcid;
                            // skip over player and corpse entries
                            if(wcid != 1 && wcid != 21)
                            {
                                uint guid = parsed.object_id;
                                string name = parsed.wdesc._name.ToString();
                                string key = wcid + ",\"" + name + "\"";

                                createdObjects.Add(guid, wcid);
                                createdObjectNames.Add(guid, name);

                                if (!CreatedWCIDs.ContainsKey(key))
                                {
                                    CreatedWCIDs.Add(key, 1);
                                    CreatedWCIDsLogs.Add(key, fileName);
                                }
                                else
                                {
                                    CreatedWCIDs[key] += 1;
                                }
                            }
                        }

                        if (messageCode == 0xF7B0) // Game Event
                        {
                            var character = fragDataReader.ReadUInt32(); // Character
                            var sequence = fragDataReader.ReadUInt32(); // Sequence
                            var _event = fragDataReader.ReadUInt32(); // Event

                            if (_event == 0x00C9) // SetAppraiseInfo
                            {
                                var parsed = CM_Examine.SetAppraiseInfo.read(fragDataReader);

                                uint success = parsed.i_prof.success_flag;
                                if (success > 0)
                                {
                                    //uint wcid = parsed.i_prof._strStatsTable[]
                                    //string name = parsed.wdesc._name.ToString();
                                    uint guid = parsed.i_objid;
                                    if (createdObjects.ContainsKey(guid))
                                    {
                                        uint wcid = createdObjects[guid];
                                        string name = createdObjectNames[guid];
                                        string key = wcid + ",\"" + name + "\"";

                                        if (!AppraisedWCIDs.ContainsKey(key))
                                        {
                                            AppraisedWCIDs.Add(key, 1);
                                            AppraisedWCIDsLogs.Add(key, fileName);
                                        }
                                        else
                                        {
                                            AppraisedWCIDs[key] += 1;
                                        }
                                    }
                                }
                            }
                        }

                        /*if (messageCode == 0xF7B1) // Game Action
                        {
                        }*/

                        /*if (messageCode == 0xF7DE) // TurbineChat
                        {
                            var parsed = CM_Admin.ChatServerData.read(fragDataReader);

                            string output = parsed.TurbineChatType.ToString("X2");

                            if (!specialOutputHits.ContainsKey(output))
                            {
                                if (specialOutputHits.TryAdd(output, 0))
                                    specialOutputHitsQueue.Enqueue(output);
                            }
                        }*/

                        /*if (messageCode == 0xF7E0) // Server Message
                        {
                            var parsed = CM_Communication.TextBoxString.read(fragDataReader);

                            //var output = parsed.ChatMessageType.ToString("X4") + " " + parsed.MessageText + ",";
                            var output = parsed.ChatMessageType.ToString("X4");

                            if (!specialOutputHits.ContainsKey(output))
                            {
                                if (specialOutputHits.TryAdd(output, 0))
                                    specialOutputHitsQueue.Enqueue(output);
                            }
                        }*/
                    }
                    catch
                    {
                        // Do something with the exception maybe
                        //exceptions++;

                        //Interlocked.Increment(ref totalExceptions);
                    }
                }
            }

            //Interlocked.Increment(ref filesProcessed);

            //processFileResuts.Add(new ProcessFileResut() { FileName = fileName, Hits = hits, Exceptions = exceptions });
        }

        private void timer1_Tick(object sender, EventArgs e)
        {
            ProcessFileResut result;
            while (!processFileResuts.IsEmpty)
            {
                if (processFileResuts.TryTake(out result))
                {
                    var length = new FileInfo(result.FileName).Length;
                    /*
                    if (result.Hits > 0 || result.Exceptions > 0)
                        dataGridView1.Rows.Add(result.Hits, result.Exceptions, length, result.FileName);
                        */
                }
            }

            string specialOutputHitsQueueResult;
            while (!specialOutputHitsQueue.IsEmpty)
            {
                if (specialOutputHitsQueue.TryDequeue(out specialOutputHitsQueueResult))
                    richTextBox1.Text += specialOutputHitsQueueResult + Environment.NewLine;
            }

            toolStripStatusLabel1.Text = "Files Processed: " + filesProcessed.ToString("N0") + " of " + filesToProcess.Count.ToString("N0");

            toolStripStatusLabel2.Text = "Total Hits: " + totalHits.ToString("N0");

            toolStripStatusLabel3.Text = "Frag Exceptions: " + totalExceptions.ToString("N0");
        }


        private void dataGridView1_CellContentDoubleClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex == -1)
                return;

            var fileName = (string)dataGridView1.Rows[e.RowIndex].Cells[3].Value;

            System.Diagnostics.Process.Start(Application.ExecutablePath, '"' + fileName + '"' + " " + opCodeToSearchFor);
        }

        private void txtOpcode_KeyPress(object sender, KeyPressEventArgs e)
        {
            if (e.KeyChar == (char)Keys.Enter)
            {
                e.Handled = true;
                btnStartSearch.PerformClick();
            }
        }
    }
}
