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
using static CM_Physics.PublicWeenieDesc;

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

        //private Dictionary<MaterialType, uint> Materials = new Dictionary<MaterialType, uint>();
        // key is WCID+","+Name of the speaker
        // val is MessageText+","+ChatMessageType of text
        //private Dictionary<string, string> Speech = new Dictionary<string, string>();

        // key is [Name of Container] + "," + WCID + "," + [LootName]
        // val is the number of hits
        private List<uint> foundIIDs = new List<uint>();
        private List<uint> foundWCIDs = new List<uint>();

        DateTime dt = DateTime.Now;

        private string logFileName { get { return "D:\\Source\\VendorAppraisal-" + dt.ToString("yyyy-MM-dd") + ".csv"; } }

        private void ResetLogFile()
        {
            using (StreamWriter theFile = new StreamWriter(logFileName, false))
            {
                theFile.WriteLine("wcid,name,logfile,item,appraisal");
            }
        }

        private void SaveResultsToLogFile(List<string> results)
        {
            if (results.Count == 0) return;

            using (StreamWriter theFile = new StreamWriter(logFileName, true))
            {
                for (var i = 0; i < results.Count; i++)
                    theFile.WriteLine(results[i]);
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
                theFile.WriteLine("Appraised Items Found: " + foundWCIDs.Count.ToString());
            }
        }

        // Gets a CSV string containing the info we are looking for!
        private string GetValueFromCreateObj(CM_Physics.CreateObject item, CM_Physics.CreateObject wielder) {
            string value = "";
            // WCID,Name,Wield WCID,Wield Name
            value = wielder.wdesc._wcid.ToString() + ",\"" + wielder.wdesc._name + "\"," + item.wdesc._wcid.ToString() + ",\"" + item.wdesc._name + "\"";
            return value;
        }

        private void ProcessFile(string fileName)
        {
            var watch = System.Diagnostics.Stopwatch.StartNew();
            int hits = 0;
            int exceptions = 0;

            var records = PCapReader.LoadPcap(fileName, true, ref searchAborted);

            Dictionary<uint, CM_Examine.SetAppraiseInfo> AppraisalList = new Dictionary<uint, CM_Examine.SetAppraiseInfo>(); // key is objectId
            Dictionary<uint, CM_Vendor.ItemProfile> CreateObjectList = new Dictionary<uint, CM_Vendor.ItemProfile>(); // key is objectId

            // Store out text to dump in here... So just one write call per log
            List<string> results = new List<string>();

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

                    // Store all the created weenies!
                    switch (opcode)
                    {
                        case PacketOpcode.APPRAISAL_INFO_EVENT:
                            var appraisalMessage = CM_Examine.SetAppraiseInfo.read(messageDataReader);
                            uint appraisalID = appraisalMessage.i_objid;
                            if (CreateObjectList.ContainsKey(appraisalID))
                            {
                                var wcid = CreateObjectList[appraisalID].pwd._wcid;
                                if(foundWCIDs.IndexOf(wcid) == -1)
                                {
                                    AddToResults(appraisalMessage, CreateObjectList[appraisalID], fileName);
                                    results.Add(AddToResults(appraisalMessage, CreateObjectList[appraisalID], fileName));
                                    foundWCIDs.Add(wcid);
                                }
                            }
                            break;
                        case PacketOpcode.VENDOR_INFO_EVENT:
                            var vendorInfoMessage = CM_Vendor.gmVendorUI.read(messageDataReader);
                            foreach(var v in vendorInfoMessage.shopItemProfileList.list)
                            {
                                var guid = v.iid;
                                var wcid = v.pwd._wcid;
                                if (v.amount == 0x00FFFFFF) // Unlimited!
                                {
                                    // Check if we already have the item or not
                                    if(foundWCIDs.IndexOf(wcid) == -1)
                                    {
                                        CreateObjectList.Add(guid, v);
                                    }
                                }
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

            watch.Stop();
            var elapsedMs = watch.ElapsedMilliseconds;
            if (watch.Elapsed.Minutes > 1)
            {
                string outputLine = watch.Elapsed.Minutes.ToString() + " Minutes for Log file " + fileName;
                richTextBox1.AppendText(outputLine + "\r\n");
            }

            if(results.Count > 0)
                SaveResultsToLogFile(results);

            Interlocked.Increment(ref filesProcessed);


            //processFileResults.Add(new ProcessFileResult() { FileName = fileName, Hits = hits, Exceptions = exceptions });
        }

        private string AddToResults(CM_Examine.SetAppraiseInfo appraise, CM_Vendor.ItemProfile itemProfile, string theLogFilename)
        {
            string result = "";

            var appraiseJSON = Newtonsoft.Json.JsonConvert.SerializeObject(appraise).Replace("\"","\"\"");
            var itemJSON = Newtonsoft.Json.JsonConvert.SerializeObject(itemProfile).Replace("\"", "\"\"");

            var wcid = itemProfile.pwd._wcid;
            var name = itemProfile.pwd._name;
            result = $"{wcid},\"{name}\",{theLogFilename},\"{appraiseJSON}\",\"{itemJSON}\"";

            return result;
        }

        private uint GetParentWeenieFromCorpse(CM_Physics.CreateObject co, Dictionary<uint, CM_Physics.CreateObject> CreateObjectList, Dictionary<uint, Position> Positions)
        {




            return 0;
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
