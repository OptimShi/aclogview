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
        private Dictionary<string, uint> Loot = new Dictionary<string, uint>();

        private string logFileName = "D:\\Source\\Treasure.csv";

        private void ResetLogFile()
        {
            using (StreamWriter theFile = new StreamWriter(logFileName, false))
                theFile.WriteLine("container guid, container wcid, container name, loot guid, loot wcid, loot name, value, material, workmanship,num tinks, gem count, gem material, spells");
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
                theFile.WriteLine("Loot Entries: " + Loot.Count.ToString());
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
            int hits = 0;
            int exceptions = 0;

            var records = PCapReader.LoadPcap(fileName, true, ref searchAborted);

            Dictionary<uint, CM_Examine.SetAppraiseInfo> AppraisalList = new Dictionary<uint, CM_Examine.SetAppraiseInfo>(); // key is objectId
            Dictionary<uint, CM_Physics.CreateObject> CreateObjectList = new Dictionary<uint, CM_Physics.CreateObject>(); // key is objectId
            Dictionary<uint, CM_Inventory.ViewContents> ViewContentsList = new Dictionary<uint, CM_Inventory.ViewContents>(); // key is ContainerID

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
                        case PacketOpcode.Evt_Physics__CreateObject_ID:
                            var message = CM_Physics.CreateObject.read(messageDataReader);
                            uint objectId = message.object_id;
                            CreateObjectList.Add(objectId, message);
                            break;
                        case PacketOpcode.APPRAISAL_INFO_EVENT:
                            var appraisalMessage = CM_Examine.SetAppraiseInfo.read(messageDataReader);
                            uint appraisalID = appraisalMessage.i_objid;
                            AppraisalList.Add(appraisalID, appraisalMessage);
                            break;
                        case PacketOpcode.VIEW_CONTENTS_EVENT:
                            var viewContentsMessage = CM_Inventory.ViewContents.read(messageDataReader);
                            uint containerId = viewContentsMessage.i_container;
                            ViewContentsList.Add(containerId, viewContentsMessage);
                            break;
                    }

                    /*
                    if(opcode == PacketOpcode.VIEW_CONTENTS_EVENT)
                    {
                        var message = CM_Inventory.ViewContents.read(messageDataReader);
                        // Check if we know what this item is and it is a Corpse
                        if (Weenies.ContainsKey(message.i_container) && WCIDs[message.i_container] == 21)
                        {
                            string container = Weenies[message.i_container];

                            // key is [Name of Container] + "," + WCID + "," + [LootName]
                            // val is the number of hits
                            for(int i = 0; i<message.contents_list.list.Count; i++)
                            {
                                var item = message.contents_list.list[i];
                                // We've captured the CreateObject message for this item
                                if (Weenies.ContainsKey(item.m_iid))
                                {
                                    container = container.Replace("Corpse of ", "");
                                    container = container.Replace("Treasure of ", "");
                                    string key = container + "," + Weenies[item.m_iid] + "";
                                    if (!Loot.ContainsKey(key))
                                    {
                                        Loot.Add(key, 1);
                                    }
                                    else
                                    {
                                        Loot[key]++;
                                    }
                                }
                            }
                        }
                    }
                    */
                }
                catch
                {
                    // Do something with the exception maybe
                    exceptions++;

                    Interlocked.Increment(ref totalExceptions);
                }
            }

            // Store out text to dump in here... So just one write call per log
            List<string> results = new List<string>();

            //container guid, container wcid, container name, loot guid, loot wcid, loot name, value, material, workmanship,num tinks, gem count, gem material, spells

            // Let's process!
            foreach (var e in ViewContentsList)
            {
                var containerId = e.Key;
                // We know what this container is...
                if(CreateObjectList.ContainsKey(containerId))
                {
                    var newObj = CreateObjectList[containerId];
                    // Make sure the container doesn't have a container id (e.g. it's a Pack in a player's inventory...)
                    if ((newObj.wdesc.header & (uint)PublicWeenieDescPackHeader.PWD_Packed_ContainerID) == 0)
                    {
                        string containerName = newObj.wdesc._name.m_buffer;
                        containerName = containerName.Replace("Corpse of ", "");
                        containerName = containerName.Replace("Treasure of ", "");

                        string containerWCID;
                        if (newObj.wdesc._wcid == 21) // Remove the "Corpse" weenies
                            containerWCID = "";
                        else
                            containerWCID = newObj.wdesc._wcid.ToString();


                            // Cycle through all the contents of the container
                            for (int i = 0; i < e.Value.contents_list.list.Count; i++)
                            {
                                var thisContent = e.Value.contents_list.list[i];
                                uint thisContentGUID = thisContent.m_iid;

                                if (CreateObjectList.ContainsKey(thisContentGUID) && AppraisalList.ContainsKey(thisContentGUID))
                                {
                                    var co = CreateObjectList[thisContentGUID];
                                    var app = AppraisalList[thisContentGUID];

                                    string lootName = co.wdesc._name.m_buffer;
                                    uint lootWCID = co.wdesc._wcid;
                                    uint value = co.wdesc._value;
                                    uint materialId = (uint)co.wdesc._material_type;
                                    if (materialId > 0)
                                    {
                                        string workmanship = "";
                                        if (app.i_prof._intStatsTable.hashTable.ContainsKey(STypeInt.ITEM_WORKMANSHIP_INT))
                                            workmanship = app.i_prof._intStatsTable.hashTable[STypeInt.ITEM_WORKMANSHIP_INT].ToString();

                                        string numTinks = "";
                                        if (app.i_prof._intStatsTable.hashTable.ContainsKey(STypeInt.NUM_TIMES_TINKERED_INT))
                                            numTinks = app.i_prof._intStatsTable.hashTable[STypeInt.NUM_TIMES_TINKERED_INT].ToString();

                                        string gemCount = "";
                                        if (app.i_prof._intStatsTable.hashTable.ContainsKey(STypeInt.GEM_COUNT_INT))
                                            gemCount = app.i_prof._intStatsTable.hashTable[STypeInt.GEM_COUNT_INT].ToString();

                                        string gemMaterial = "";
                                        if (app.i_prof._intStatsTable.hashTable.ContainsKey(STypeInt.GEM_TYPE_INT))
                                            gemMaterial = app.i_prof._intStatsTable.hashTable[STypeInt.GEM_TYPE_INT].ToString();

                                        string spells = "";
                                        for (var j = 0; j < app.i_prof._spellsTable.list.Count; j++)
                                            spells += app.i_prof._spellsTable.list[j].ToString() + "|";

                                        string result = thisContentGUID + "," +
                                            containerWCID + "," +
                                            containerName + "," +
                                            thisContentGUID + "," +
                                            lootWCID + "," +
                                            lootName + "," +
                                            value + "," +
                                            materialId + "," +
                                            workmanship + "," +
                                            numTinks + "," +
                                            gemCount + "," +
                                            gemMaterial + "," +
                                            spells;
                                        results.Add(result);
                                }
                            }
                        }
                    }
                }
            }

            if(results.Count > 0)
                SaveResultsToLogFile(results);

            Interlocked.Increment(ref filesProcessed);


            //processFileResults.Add(new ProcessFileResult() { FileName = fileName, Hits = hits, Exceptions = exceptions });
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
