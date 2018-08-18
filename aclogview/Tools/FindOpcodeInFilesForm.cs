using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

using aclogview.Properties;
using System.Text;
using System.Diagnostics;

namespace aclogview
{
    public partial class FindOpcodeInFilesForm : Form
    {
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

        private class ProcessFileResult
        {
            public string FileName;
            public int Hits;
            public int Exceptions;
        }

        private readonly ConcurrentBag<ProcessFileResult> processFileResults = new ConcurrentBag<ProcessFileResult>();
        
        private readonly ConcurrentDictionary<string, int> specialOutputHits = new ConcurrentDictionary<string, int>();
        private readonly ConcurrentQueue<string> specialOutputHitsQueue = new ConcurrentQueue<string>();

        private List<uint> Dungeons = new List<uint>();
        private Dictionary<string, uint> FoundSpawns = new Dictionary<string, uint>();

        private string logFileName = "D:\\Source\\PathOfTheBlind.csv";

        private void ResetLogFile()
        {
            using (StreamWriter theFile = new StreamWriter(logFileName, false))
                theFile.WriteLine("WCID,Name,@Loc,Hits");
        }

        private void SaveResultsToLogFile()
        {
            using (StreamWriter theFile = new StreamWriter(logFileName, true))
            {
                foreach (KeyValuePair<string, uint> entry in FoundSpawns)
                {
                    theFile.WriteLine(entry.Key + "," + entry.Value.ToString());
                }
            }
        }
        private void btnStartSearch_Click(object sender, EventArgs e)
        {
            dataGridView1.RowCount = 0;
            
            Dungeons.Add(0x002A); // Path of the Blind
            Dungeons.Add(0x004B); // Path of the Blind
            Dungeons.Add(0x00E1); // Path of the Blind
            Dungeons.Add(0x02CD); // Floating City for Testing
            try
            {
                btnStartSearch.Enabled = false;

                filesToProcess.Clear();
                opCodeToSearchFor = OpCode;
                filesProcessed = 0;
                totalHits = 0;
                totalExceptions = 0;
                searchAborted = false;

                ProcessFileResult result;
                while (!processFileResults.IsEmpty)
                    processFileResults.TryTake(out result);

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

        private void DoSearch()
        {
            int progress = 0;
            // Parallel.ForEach(filesToProcess, (currentFile) =>
            // string currentFile = "D:\\ACE\\Logs\\PCAP Part 1\\Floating-City\\pkt_2017-1-31_1485869055_log.pcap";
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
            using (StreamWriter theFile = new StreamWriter("D:\\Source\\Dungeon_Search_Progress.txt", false))
            {
                theFile.WriteLine(progress.ToString() + " of " + total.ToString());
                theFile.WriteLine(filename);
                theFile.WriteLine("FoundSpawns: " + FoundSpawns.Count.ToString());
            }
        }

        // Gets a CSV string containing the info we are looking for!
        private string GetValueFromCreateObj(CM_Physics.CreateObject co) {
            string value = "";
            //WCID,NAME,<@loc syntax>
            value = co.wdesc._wcid.ToString() + "," + co.wdesc._name + ",\"" + GetLoc(co) + "\"";
            return value;
        }

        private string GetLoc(CM_Physics.CreateObject co)
        {
            string loc;
            var pos = co.physicsdesc.pos;
            string objCell = "0x" + pos.objcell_id.ToString("X8");

            // @loc 0x00070131 [64.9584 -44.8534 0.66] 0 0 0 -1
            loc = objCell + " [" + pos.frame.m_fOrigin.x.ToString() + " " + pos.frame.m_fOrigin.y.ToString() + " " + pos.frame.m_fOrigin.z.ToString() + "] ";
            loc += pos.frame.qx.ToString() + " " + pos.frame.qy.ToString() + " " + pos.frame.qz.ToString() + " " + pos.frame.qw.ToString();

            return loc;
        }

        private bool IsInDungeon(CM_Physics.CreateObject co)
        {
            uint fullCell = co.physicsdesc.pos.objcell_id;
            uint objCell = fullCell >> 16;
            if (Dungeons.IndexOf(objCell) != -1)
                return true;
            else
                return false;
        }

        private void ProcessFile(string fileName)
        {
            int hits = 0;
            int exceptions = 0;

            var records = PCapReader.LoadPcap(fileName, true, ref searchAborted);

            Dictionary<uint, CM_Physics.CreateObject> items = new Dictionary<uint, CM_Physics.CreateObject>();

            foreach (PacketRecord record in records)
            {
                if (searchAborted || Disposing || IsDisposed)
                    return;

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

                    var messageCode = messageDataReader.ReadUInt32();

                    if (messageCode == 0xF745) // Create Object
                    {
                        var parsed = CM_Physics.CreateObject.read(messageDataReader);
                        uint wcid = parsed.wdesc._wcid;

                        // item is not a player, is a creature and is in our dungeon...
                        if (wcid > 1 && IsInDungeon(parsed))
                        {
                            // string index = GetValueFromCreateObj(parsed);
                            uint key = parsed.object_id;
                            items.Add(key, parsed);
                        }
                    }

                    if (messageCode == 0xF755) // Effects_PlayScriptType 
                    {
                        var parsed = CM_Physics.PlayScriptType.read(messageDataReader);
                        if(parsed.script_type == PScriptType.PS_Create)
                        {
                            uint objId = parsed.object_id;
                            if (items.ContainsKey(objId)) {
                                var createObj = items[objId];
                                string value = GetValueFromCreateObj(createObj);

                                if (FoundSpawns.ContainsKey(value))
                                    FoundSpawns[value]++;
                                else
                                    FoundSpawns.Add(value, 1);
                            }
                        }
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

            processFileResults.Add(new ProcessFileResult() { FileName = fileName, Hits = hits, Exceptions = exceptions });
        }

        private void timer1_Tick(object sender, EventArgs e)
        {
            ProcessFileResult result;
            while (!processFileResults.IsEmpty)
            {
                if (processFileResults.TryTake(out result))
                {
                    var length = new FileInfo(result.FileName).Length;

                    if (result.Hits > 0 || result.Exceptions > 0)
                        dataGridView1.Rows.Add(result.Hits, result.Exceptions, length, result.FileName);
                }
            }

            string specialOutputHitsQueueResult;
            StringBuilder specialOutput = new StringBuilder();
            while (!specialOutputHitsQueue.IsEmpty)
            {
                if (specialOutputHitsQueue.TryDequeue(out specialOutputHitsQueueResult))
                    specialOutput.AppendLine(specialOutputHitsQueueResult);
            }
            richTextBox1.AppendText(specialOutput.ToString());

            toolStripStatusLabel1.Text = "Files Processed: " + filesProcessed.ToString("N0") + " of " + filesToProcess.Count.ToString("N0");

            toolStripStatusLabel2.Text = "Total Hits: " + totalHits.ToString("N0");

            toolStripStatusLabel3.Text = "Message Exceptions: " + totalExceptions.ToString("N0");
        }

        private void dataGridView1_CellDoubleClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex == -1)
                return;

            var fileName = (string)dataGridView1.Rows[e.RowIndex].Cells[3].Value;

            System.Diagnostics.Process.Start(Application.ExecutablePath, "-f" + '"' + fileName + '"' + " -o " + opCodeToSearchFor);
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
