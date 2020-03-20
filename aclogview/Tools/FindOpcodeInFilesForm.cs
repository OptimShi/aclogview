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

        //private Dictionary<MaterialType, uint> Materials = new Dictionary<MaterialType, uint>();
        // key is "{Guid},{WCID},{Name}
        // val is Comma seperated values of all the other details...
        private List<string> NPCs = new List<string>();

        private string logFileName = "D:\\Source\\NPC-Appearance.csv";

        private void ResetLogFile()
        {
            using (StreamWriter theFile = new StreamWriter(logFileName, false))
                theFile.WriteLine("Guid,WCID,Name,HeadGfxObj,SkinColor,HairColor,EyeColor,EyeStrip,NoseStrip,MouthStrip");
        }

        private void SaveResultsToLogFile()
        {
            ResetLogFile();
            using (StreamWriter theFile = new StreamWriter(logFileName, true))
            {
                for(var i = 0;i<NPCs.Count;i++){
                    theFile.WriteLine(NPCs[i]);
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
                SaveResultsToLogFile();
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
                theFile.WriteLine("NPC Entries: " + NPCs.Count.ToString());
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
            int exceptions = 0;

            var records = PCapReader.LoadPcap(fileName, true, ref searchAborted);

            string myPath = "D:\\Asheron's Call\\Log Files\\";
            string logFilenameVal = fileName.Replace(myPath, "");

            // Key is the landblock. Values is a list of all the seen objcell ids
            Dictionary<uint, List<uint>> Stabs = new Dictionary<uint, List<uint>>();

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

                    switch (opcode) {
                        case PacketOpcode.Evt_Physics__CreateObject_ID:
                            var msg = CM_Physics.CreateObject.read(messageDataReader);

                            var guid = msg.object_id;
                            var wcid = msg.wdesc._wcid;
                            var name = msg.wdesc._name.m_buffer.Replace("\"", "\"\""); // escape the slashes for CSV output
                            var itemtype = msg.wdesc._type;

                            bool isVendor = ((msg.wdesc._bitfield & 512) != 0); // BF_VENDOR bitfield

                            if (itemtype == ITEM_TYPE.TYPE_CREATURE && wcid != 1 && wcid != 21 && (msg.wdesc._blipColor == 8 || isVendor))
                            {
                                string baseData = $"{guid},{wcid},\"{name}\",";
                                // Guid,WCID,Name,HeadGfxObj,SkinColor,HairColor,EyeColor,EyeStrip,NoseStrip,MouthStrip

                                // get HeadGfxObj
                                string headGfxObj = "";
                                foreach (var o in msg.objdesc.apChanges)
                                {
                                    if (o.part_index == 0x10)
                                    {
                                        headGfxObj = o.part_id.ToString("X8");
                                        break;
                                    }
                                }

                                string skinColor = "";
                                string hairColor = "";
                                string eyeColor = "";
                                foreach (var s in msg.objdesc.subpalettes)
                                {
                                    if (s.offset == 0 && s.numcolors == 24)
                                        skinColor = s.subID.ToString("X8");
                                    if (s.offset == 24 && s.numcolors == 8)
                                        hairColor = s.subID.ToString("X8");
                                    if (s.offset == 32 && s.numcolors == 8)
                                        eyeColor = s.subID.ToString("X8");
                                }

                                string eyeStrip = "";
                                string noseStrip = "";
                                string mouthStrip = "";
                                foreach (var t in msg.objdesc.tmChanges)
                                {
                                    if (t.old_tex_id == 0x500024C)
                                        eyeStrip = t.new_tex_id.ToString("X8");
                                    if (t.old_tex_id == 0x50002F5)
                                        noseStrip = t.new_tex_id.ToString("X8");
                                    if (t.old_tex_id == 0x500025C)
                                        mouthStrip = t.new_tex_id.ToString("X8");
                                }

                                if (skinColor != "" && hairColor != "" && eyeColor != "")
                                {
                                    string appearanceData = baseData + $"{headGfxObj},{skinColor},{hairColor},{eyeColor},{eyeStrip},{noseStrip},{mouthStrip}";
                                    NPCs.Add(appearanceData);
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

            Interlocked.Increment(ref filesProcessed);

            if(Stabs.Count > 0)
            {
                using (StreamWriter theFile = new StreamWriter(logFileName, true))
                {
                    foreach (KeyValuePair<uint, List<uint>> e in Stabs)
                    {
                        if (e.Value.Count > 0)
                        {
                            theFile.Write(e.Key.ToString("X8") + ",");
                            theFile.Write(fileName + ",");
                            for (var i = 0; i < e.Value.Count; i++)
                            {
                                theFile.Write(e.Value[i].ToString("X8") + ",");
                            }
                            theFile.WriteLine();
                        }
                    }
                }
            }

            Stabs.Clear();


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
