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

        private Dictionary<string, uint> CreatedWCIDs = new Dictionary<string, uint>(); // key is "wcid,name", value is times found
        private Dictionary<string, string> CreatedWCIDsLogs = new Dictionary<string, string>(); // key is "wcid,name", value is first log found in
        private Dictionary<string, uint> AppraisedWCIDs = new Dictionary<string, uint>(); // key is "wcid,name", value is times found
        private Dictionary<string, string> AppraisedWCIDsLogs = new Dictionary<string, string>(); // key is "wcid,name", value is first log found in

        private string logFileName = "D:\\Source\\WCIDs-" + DateTime.Today.ToString("yyyy-MM-dd") + ".csv";

        private void ResetLogFile()
        {
            using (StreamWriter theFile = new StreamWriter(logFileName, false))
                theFile.WriteLine("WCID,Name,EventType,Hits,LogFile");
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
            }
        }

        private bool isPcapng;
        private double startTimer;
        private string currentTimer;

        private void ProcessFile(string fileName)
        {
            int exceptions = 0;
            //var result = parser.ProcessFileRecords(currentFile, records, ref searchAborted, opCodeToSearchFor);
            var records = PCapReader.LoadPcap(fileName, true, ref searchAborted, out isPcapng);

            string myPath = "D:\\Asheron's Call\\Log Files\\";
            string logFilenameVal = fileName.Replace(myPath, "");

            string quot = "\"";

            Dictionary<uint, uint> createdObjects = new Dictionary<uint, uint>(); // key is guid, value is wcid
            Dictionary<uint, string> createdObjectNames = new Dictionary<uint, string>(); // key is guid, value is name

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
                        case PacketOpcode.PLAYER_DESCRIPTION_EVENT:
                            createdObjects.Clear();
                            createdObjectNames.Clear();
                            break;
                        case PacketOpcode.Evt_Physics__CreateObject_ID: // Stores the guid => wcid/name of all created items so we can reference it
                            var createMsg = CM_Physics.CreateObject.read(messageDataReader);
                            uint wcid = createMsg.wdesc._wcid;
                            // skip over player and corpse entries
                            if (wcid != 1 && wcid != 21)
                            {
                                uint guid = createMsg.object_id;
                                string name = createMsg.wdesc._name.m_buffer;
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
                            break;
                        case PacketOpcode.APPRAISAL_INFO_EVENT:
                            var parsed = CM_Examine.SetAppraiseInfo.read(messageDataReader);

                            uint success = parsed.i_prof.success_flag;
                            if (success > 0)
                            {
                                //uint wcid = parsed.i_prof._strStatsTable[]
                                //string name = parsed.wdesc._name.ToString();
                                uint guid = parsed.i_objid;
                                if (createdObjects.ContainsKey(guid))
                                {
                                    uint _wcid = createdObjects[guid];
                                    string name = createdObjectNames[guid];
                                    string key = _wcid + ",\"" + name + "\"";

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
                            break;
                        case PacketOpcode.Evt_Physics__DeleteObject_ID:
                            var deleteMessage = CM_Physics.DeleteObject.read(messageDataReader);
                            var deleteObjectId = deleteMessage.object_id;
                            if (createdObjects.ContainsKey(deleteObjectId))
                            {
                                createdObjects.Remove(deleteObjectId);
                                createdObjectNames.Remove(deleteObjectId);
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

            //processFileResults.Add(new ProcessFileResult() { FileName = fileName, Hits = hits, Exceptions = exceptions });
        }
    }
}
