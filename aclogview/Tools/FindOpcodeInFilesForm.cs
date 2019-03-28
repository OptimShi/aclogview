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



        private string logFileName = "D:\\Source\\AppraisalProps.csv";
        private List<int> intStats = new List<int>();
        private List<int> int64Stats = new List<int>();
        private List<int> boolStats = new List<int>();
        private List<int> floatStats = new List<int>();
        private List<int> strStats = new List<int>();
        private List<int> didStats = new List<int>();

        private void ResetLogFile()
        {
            using (StreamWriter theFile = new StreamWriter(logFileName, false))
                theFile.WriteLine("Int,Int64,Bool,Float,Str,Did");
        }

        private void SaveResultsToLogFile()
        {
            using (StreamWriter theFile = new StreamWriter(logFileName, false))
            {
                theFile.WriteLine("Int,,Int64,,Bool,,Float,,Str,,Did");

                // get the max count
                int max = 0;
                if (intStats.Count > max) max = intStats.Count;
                if (int64Stats.Count > max) max = int64Stats.Count;
                if (boolStats.Count > max) max = boolStats.Count;
                if (floatStats.Count > max) max = floatStats.Count;
                if (strStats.Count > max) max = strStats.Count;
                if (didStats.Count > max) max = didStats.Count;

                for(int i = 0; i < max; i++)
                {
                    if (intStats.Count > i) theFile.Write(intStats[i] + "," + (STypeInt)intStats[i] + ","); else theFile.Write(",,");
                    if (int64Stats.Count > i) theFile.Write(int64Stats[i] + "," + (STypeInt64)int64Stats[i] + ","); else theFile.Write(",,");
                    if (boolStats.Count > i) theFile.Write(boolStats[i] + "," + (STypeBool)boolStats[i] + ","); else theFile.Write(",,");
                    if (floatStats.Count > i) theFile.Write(floatStats[i] + "," + (STypeFloat)floatStats[i] + ","); else theFile.Write(",,");
                    if (strStats.Count > i) theFile.Write(strStats[i] + "," + (STypeString)strStats[i] + ","); else theFile.Write(",,");
                    if (didStats.Count > i) theFile.Write(didStats[i] + "," + (STypeDID)didStats[i] + ","); else theFile.Write(",,");

                    theFile.WriteLine();
                }
            }
        }

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
                    SaveResultsToLogFile();
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

        private void ProcessFile(string fileName)
        {
            int exceptions = 0;

            var records = PCapReader.LoadPcap(fileName, true, ref searchAborted);

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

                    PacketOpcode opcode = Util.readOpcode(messageDataReader);

                    if(opcode == PacketOpcode.APPRAISAL_INFO_EVENT)
                    {
                        var message = CM_Examine.SetAppraiseInfo.read(messageDataReader);
                        foreach (KeyValuePair<STypeInt, int> entry in message.i_prof._intStatsTable.hashTable)
                            if (intStats.IndexOf((int)entry.Key) == -1) intStats.Add((int)entry.Key);

                        foreach (KeyValuePair<STypeInt64, long> entry in message.i_prof._int64StatsTable.hashTable)
                            if (int64Stats.IndexOf((int)entry.Key) == -1) int64Stats.Add((int)entry.Key);

                        foreach (KeyValuePair<STypeBool, int> entry in message.i_prof._boolStatsTable.hashTable)
                            if (boolStats.IndexOf((int)entry.Key) == -1) boolStats.Add((int)entry.Key);

                        foreach (KeyValuePair<STypeFloat, double> entry in message.i_prof._floatStatsTable.hashTable)
                            if (floatStats.IndexOf((int)entry.Key) == -1) floatStats.Add((int)entry.Key);

                        foreach (KeyValuePair<STypeString, PStringChar> entry in message.i_prof._strStatsTable.hashTable)
                            if (strStats.IndexOf((int)entry.Key) == -1) strStats.Add((int)entry.Key);

                        foreach (KeyValuePair<STypeDID, uint> entry in message.i_prof._didStatsTable.hashTable)
                            if (didStats.IndexOf((int)entry.Key) == -1) didStats.Add((int)entry.Key);
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
        }

        private void timer1_Tick(object sender, EventArgs e)
        {
            
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
