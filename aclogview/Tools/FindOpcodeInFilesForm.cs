using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;

using aclogview.Properties;
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

        public List<int> SpellsToFind = new List<int>(){
                3581, // Secrets of Liazk Itzi's Temple
                3826, // a powerful force
                3846, // Ruined Access
                3847, // Cataracts of Xik Minru
                3865, // Glenden Wood Recall
                3868, // Dardante's Keep Portal Sending
                3887, // Entering the Hatch
                3888, // Passage to the Rare Chambers
                3889, // Inner Burial Chamber Portal Sending
                3898, // Pooky's Recall 1
                3899, // Pooky's Recall 2
                3900, // Pooky's Recall 3
                3912, // Lower Black Spear Temple Portal Sending
                3920, // Tunnels to the Harbinger
                3921, // Harbinger's Lair
                3922, // Tunnels to the Harbinger
                3923, // Tunnels to the Harbinger
                3924, // Tunnels to the Harbinger
                3925, // Harbinger's Lair
                3929, // Rossu Morta Chapterhouse Recall
                3930, // Whispering Blade Chapterhouse Recall
                3954, // Access to the White Tower
                3958, // White Tower Egress
                3966, // Ringleader's Chambers
                3967, // Bandit Trap
                3968, // Bandit Hideout
                4012, // White Totem Temple Sending
                4013, // Black Totem Temple Sending
                4014, // Abyssal Totem Temple Sending
                4023, // Disco Inferno Portal Sending
                4029, // Return to the Hall of Champions
                4030, // Colosseum Arena
                4031, // Advanced Colosseum Arena
                4032, // Colosseum Arena
                4033, // Advanced Colosseum Arena
                4034, // Colosseum Arena
                4035, // Advanced Colosseum Arena
                4036, // Colosseum Arena
                4037, // Advanced Colosseum Arena
                4038, // Colosseum Arena
                4039, // Advanced Colosseum Arena
                4041, // The Path to Bur
                4042, // The Winding Path to Bur
                4063, // Exit the Upper Catacomb
                4064, // Access the Upper Catacomb
                4065, // Lower Catacomb Portal Sending
                4066, // Access to the Ley Line Cavern
                4079, // Work it Off
                4080, // Paid in Full
                4083, // Kresovus' Warren Portal Sending
                4084, // Bur Recall
                4085, // Entering Harraag's Hideout
                4098, // Treasure Room
                4103, // Black Water Portal Sending
                4104, // Champion Arena
                4105, // Champion Arena
                4106, // Travel to the Paradox-touched Olthoi Queen's Lair
                4127, // Portal Punch
                4128, // Call of the Mhoire Forge
                4129, // Travel to the Prodigal Shadow Child's Lair
                4130, // Travel to the Prodigal Shadow Child's Sanctum
                4145, // Crossing the Threshold of Darkness
                4146, // Crossing the Threshold of Darkness
                4147, // Crossing the Threshold of Darkness
                4148, // Crossing the Threshold of Darkness
                4149, // Crossing the Threshold of Darkness
                4150, // Expulsion from Claude's Mind
                4176, // Prodigal Harbinger's Lair
                4177, // Prodigal Harbinger's Antechamber
                4178, // Prodigal Harbinger's Antechamber
                4179, // Prodigal Harbinger's Antechamber
                4180, // Prodigal Harbinger's Antechamber
                4198, // Paradox-touched Olthoi Infested Area Recall
                4200, // Into the Darkness
                4203, // Dark Crypt Entrance
                4204, // Dark Crypt Entrance
                4205, // Dark Crypt Entrance
                4207, // Arena of the Pumpkin King
                //4213, // Colosseum Recall
                //4214, // Return to the Keep
                4218, // Knockback
                4219, // Trial of the Arm
                4220, // Trial of the Heart
                4222, // Chambers Beneath
                4223, // Trials Graduation Chamber
                4224, // Trial of the Mind
                4225, // Trials of the Arm, Mind and Heart
                4228, // Awakening
                4229, // Journey Into the Past
                4230, // Bael'Zharon Dream Sending
                4233, // Aerbax Recall Center Platform
                4234, // Aerbax Recall East Platform
                4235, // Aerbax Recall North Platform
                4236, // Aerbax Recall South Platform
                4237, // Aerbax Recall West Platform
                4238, // Aerbax Expulsion
                4247, // Tanada Battle Burrows Portal Sending
                4248, // Shroud Cabal North Outpost Sending
                4249, // Shroud Cabal South Outpost Sending
                4250, // Aerbax's Platform
                4251, // Jester's Boot
                4252, // Entrance to the Jester's Cell
                4253, // Entrance to the Jester's Cell
                4254, // Jester's Prison Hallway
                4255, // Jester's Prison Entryway
                4256, // Jester Recall 1
                4257, // Jester Recall 2
                4258, // Jester Recall 3
                4259, // Jester Recall 4
                4260, // Jester Recall 5
                4261, // Jester Recall 6
                4262, // Jester Recall 7
                4263, // Jester Recall 8
                4277, // Jester's Prison Access
                4278, // Rytheran's Library Portal Sending
                4289, // Access the Messenger's Sanctuary
                4717, // Expedient Return to Ulgrim
                4718, // Welcomed by the Blood Witches
                4719, // Welcomed by the Blood Witches
                4720, // Welcomed by the Blood Witches
                4721, // Travel to the Ruins of Degar'Alesh
                4724, // Gateway to Nyr'leha
                4725, // The Pit of Heretics
                4729, // Travel to the Catacombs of Tar'Kelyn
                4907, // Celestial Hand Stronghold Recall
                4908, // Eldrytch Web Stronghold Recall
                4909, // Radiant Blood Stronghold Recall
                4913, // Aerlinthe Pyramid Portal Sending
                4914, // Aerlinthe Pyramid Portal Exit
                4915, // A'mun Pyramid Portal Sending
                4916, // A'mun Pyramid Portal Exit
                4917, // Esper Pyramid Portal Sending
                4918, // Esper Pyramid Portal Exit
                4919, // Halaetan Pyramid Portal Sending
                4920, // Halaetan Pyramid Portal Exit
                4921, // Linvak Pyramid Portal Sending
                4922, // Linvak Pyramid Portal Exit
                4923, // Obsidian Pyramid Portal Sending
                4924, // Obsidian Pyramid Portal Exit
                4950, // Tactical Defense
                4951, // Tactical Defense
                4952, // Tactical Defense
                4953, // Test Portal
                4954, // Crystalline Portal
                4955, // Portal Space Eddy
                4956, // Tanada Sanctum Portal Sending
                4957, // Tanada Sanctum Return
                4988, // Tunnel Out
                4989, // Mysterious Portal
                4990, // Floor Puzzle Bypass
                4991, // Jump Puzzle Bypass
                4992, // Direct Assassin Access
                4993, // Portal to Derethian Combat Arena
                4994, // Get over here!
                4995, // Portal to Derethian Combat Arena
                4996, // Portal to Derethian Combat Arena
                4997, // Portal to Derethian Combat Arena
                5008, // Apostate Nexus Portal Sending
                5010, // Entering Aerfalle's Sanctum
                5012, // Mar'uun
                5013, // Mar'uun
                5014, // Mar'uun
                5015, // Mar'uun
                5016, // Mar'uun
                5017, // Mar'uun
                5018, // Story of the Unknown Warrior
                5019, // Portalspace Rift
                5020, // Portalspace Rift
                5021, // Portalspace Rift
                5022, // Portalspace Rift
                5160, // Mhoire Castle
                5161, // Mhoire Castle Great Hall
                5162, // Mhoire Castle Northeast Tower
                5163, // Mhoire Castle Northwest Tower
                5164, // Mhoire Castle Southeast Tower
                5165, // Mhoire Castle Southwest Tower
                5167, // Mhoire Castle Exit Portal
                5168, // a spectacular view of the Mhoire lands
                5169, // a descent into the Mhoire catacombs
                5170, // a descent into the Mhoire catacombs
                5176, // Celestial Hand Basement
                5177, // Radiant Blood Basement
                5178, // Eldrytch Web Basement
                5179, // Celestial Hand Basement
                5180, // Radiant Blood Basement
                5181, // Eldrytch Web Basement
                5330, // Gear Knight Invasion Area Camp Recall
                5533, // Entering Lord Kastellar's Lab
                5534, // Entering the Bloodstone Factory
                5539, // Warded Cavern Passage
                5540, // Warded Dungeon Passage
                5541, // Lost City of Neftet Recall
                6032, // Imprisoned
                6033, // Impudence
                6034, // Proving Grounds Rolling Death
                6147, // Entrance to the Frozen Valley
                6148, // Begone and Be Afraid
                6149, // Rynthid Vision
                //6150, // Rynthid Recall
                6154, // Entering the Basement
                6183, // Return to the Stronghold
                6184, // Return to the Stronghold
                6185, // Return to the Stronghold
                6321, // Viridian Rise Recall
                6322, // Viridian Rise Great Tree Recall
                6325, // Celestial Hand Stronghold Recall
                6326, // Eldrytch Web Stronghold Recall
                6327, // Radiant Blood Stronghold Recall
            };

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
        public class RecallGroup
        {
            public SpellID spellId;
            public Dictionary<string, uint> log = new Dictionary<string, uint>();
        }

        private Dictionary<SpellID, RecallGroup> TeleportSpells = new Dictionary<SpellID, RecallGroup>();

        DateTime dt = DateTime.Now;

        private string logFileName { get { return "D:\\Source\\Teleports-" + dt.ToString("yyyy-MM-dd") + ".csv"; } }

        private void ResetLogFile()
        {
            return;
            using (StreamWriter theFile = new StreamWriter(logFileName, false))
            {
                theFile.Write("container guid, container wcid, container name, container landblock,loot guid, loot wcid, loot name, item type, weapon type, description, value, material, workmanship,num tinks, gem count, gem material,");
                theFile.WriteLine("clothingPriority,locations,wieldReq,wieldSkillType,wieldDiff,wieldReq2,wieldSkillType2,wieldDiff2,item level,spellcraft,difficulty,max mana,mana cost,spell set, spells");
            }
        }

        private void SaveResultsToLogFile(List<string> results)
        {
            return;
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
                theFile.WriteLine("Portal Spells Found: " + TeleportSpells.Count.ToString());
                foreach(var ts in TeleportSpells)
                {
                    theFile.WriteLine("*******************");
                    theFile.WriteLine(ts.Value.spellId);
                    foreach (var e in ts.Value.log)
                        theFile.WriteLine(e.Key + " record " + e.Value);
                }
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

            // Store out text to dump in here... So just one write call per log
            uint recordIndex = 0;
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
                    recordIndex++;

                    BinaryReader messageDataReader = new BinaryReader(new MemoryStream(record.data));

                    PacketOpcode opcode = Util.readOpcode(messageDataReader);

                    // Store all the created weenies!
                    switch (opcode)
                    {
                        case PacketOpcode.Evt_Magic__CastTargetedSpell_ID:
                            var targetedSpell = CM_Magic.CastTargetedSpell.read(messageDataReader);
                            int spellId = (int)targetedSpell.i_spell_id;
                            if(SpellsToFind.IndexOf(spellId) != -1)
                            {
                                if (!TeleportSpells.ContainsKey((SpellID)spellId)){
                                    TeleportSpells.Add((SpellID)spellId, new RecallGroup());
                                    TeleportSpells[(SpellID)spellId].spellId = (SpellID)spellId;
                                }
                                TeleportSpells[(SpellID)spellId].log.Add(fileName, recordIndex);
                            }
                            break;
                        /*
                        case PacketOpcode.Evt_Physics__CreateObject_ID:
                            var message = CM_Physics.CreateObject.read(messageDataReader);
                            uint objectId = message.object_id;
                            if (CreateObjectList.ContainsKey(objectId))
                            {
                                CreateObjectList[objectId] = message;
                                Positions[objectId] = message.physicsdesc.pos;
                            }
                            else
                            {
                                CreateObjectList.Add(objectId, message);
                                Positions.Add(objectId, message.physicsdesc.pos);
                            }
                            break;
                        case PacketOpcode.APPRAISAL_INFO_EVENT:
                            var appraisalMessage = CM_Examine.SetAppraiseInfo.read(messageDataReader);
                            uint appraisalID = appraisalMessage.i_objid;
                            if (AppraisalList.ContainsKey(appraisalID))
                                AppraisalList[appraisalID] = appraisalMessage;
                            else
                                AppraisalList.Add(appraisalID, appraisalMessage);
                            break;
                        case PacketOpcode.VIEW_CONTENTS_EVENT:
                            var viewContentsMessage = CM_Inventory.ViewContents.read(messageDataReader);
                            uint containerId = viewContentsMessage.i_container;
                            string getResultToAdd = AddToResults(viewContentsMessage, CreateObjectList, AppraisalList);
                            // make sure our result is not empty and not already in the list!
                            if (getResultToAdd != "" && results.IndexOf(getResultToAdd) == -1)
                                results.Add(getResultToAdd);
                            break;
                        case PacketOpcode.Evt_Movement__UpdatePosition_ID:
                            var positionMessage = CM_Movement.UpdatePosition.read(messageDataReader);
                            var positionObjId = positionMessage.object_id;
                            if (Positions.ContainsKey(positionObjId))
                                Positions[positionObjId] = positionMessage.positionPack.position;
                            else
                                Positions.Add(positionObjId, positionMessage.positionPack.position);
                            break;
                        */
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

            Interlocked.Increment(ref filesProcessed);


            //processFileResults.Add(new ProcessFileResult() { FileName = fileName, Hits = hits, Exceptions = exceptions });
        }

        private string AddToResults(CM_Inventory.ViewContents contents, Dictionary<uint, CM_Physics.CreateObject> CreateObjectList, Dictionary<uint, CM_Examine.SetAppraiseInfo> AppraisalList)
        {
            string result = "";
            var containerId = contents.i_container;
            // We know what this container is...
            if (CreateObjectList.ContainsKey(containerId))
            {
                var newObj = CreateObjectList[containerId];
                // Make sure the container doesn't have a container id (e.g. it's a Pack in a player's inventory...)
                if ((newObj.wdesc.header & (uint)PublicWeenieDescPackHeader.PWD_Packed_ContainerID) == 0)
                {
                    string containerName = newObj.wdesc._name.m_buffer;
                    containerName = containerName.Replace("Corpse of ", "");
                    containerName = containerName.Replace("Treasure of ", "");

                    string containerPos = "";
                    if((newObj.physicsdesc.bitfield & (uint)0x8000) != 0)
                        containerPos = newObj.physicsdesc.pos.objcell_id.ToString("X8");

                    //uint containerWCID = GetParentWeenieFromCorpse(newObj, CreateObjectList, Positions);
                    string containerWCID;
                    if (newObj.wdesc._wcid == 21) // Remove the "Corpse" weenies
                        containerWCID = "";
                    else
                        containerWCID = newObj.wdesc._wcid.ToString();

                    // Cycle through all the contents of the container
                    for (int i = 0; i < contents.contents_list.list.Count; i++)
                    {
                        var thisContent = contents.contents_list.list[i];
                        uint thisContentGUID = thisContent.m_iid;

                        if (CreateObjectList.ContainsKey(thisContentGUID) && AppraisalList.ContainsKey(thisContentGUID))
                        {
                            var co = CreateObjectList[thisContentGUID];
                            var app = AppraisalList[thisContentGUID];

                            string lootName = co.wdesc._name.m_buffer;
                            uint lootWCID = co.wdesc._wcid;
                            uint value = co.wdesc._value;
                            uint materialId = (uint)co.wdesc._material_type;

                            string itemLevel = "";
                            if (co.wdesc._iconOverlayID > 0)
                            {
                                switch (co.wdesc._iconOverlayID)
                                {
                                    case 0x06006C34:
                                        itemLevel = "1";
                                        break;
                                    case 0x06006c35:
                                        itemLevel = "2";
                                        break;
                                    case 0x06006c36:
                                        itemLevel = "3";
                                        break;
                                    case 0x06006c37:
                                        itemLevel = "4";
                                        break;
                                    case 0x06006c38:
                                        itemLevel = "5";
                                        break;
                                }
                            }

                            if (materialId > 0 || itemLevel != "")
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

                                string itemType = co.wdesc._type.ToString();

                                string clothingPriority = "";
                                if (co.wdesc._priority > 0)
                                    clothingPriority = co.wdesc._priority.ToString();
                                string locations = "";
                                if (co.wdesc._valid_locations > 0)
                                    locations = co.wdesc._valid_locations.ToString();

                                string wieldReq = "";
                                if (app.i_prof._intStatsTable.hashTable.ContainsKey(STypeInt.WIELD_REQUIREMENTS_INT))
                                    wieldReq = app.i_prof._intStatsTable.hashTable[STypeInt.WIELD_REQUIREMENTS_INT].ToString();
                                string wieldSkillType = "";
                                if (app.i_prof._intStatsTable.hashTable.ContainsKey(STypeInt.WIELD_SKILLTYPE_INT))
                                    wieldSkillType = app.i_prof._intStatsTable.hashTable[STypeInt.WIELD_SKILLTYPE_INT].ToString();
                                string wieldDiff = "";
                                if (app.i_prof._intStatsTable.hashTable.ContainsKey(STypeInt.WIELD_DIFFICULTY_INT))
                                    wieldDiff = app.i_prof._intStatsTable.hashTable[STypeInt.WIELD_DIFFICULTY_INT].ToString();

                                string wieldReq2 = "";
                                if (app.i_prof._intStatsTable.hashTable.ContainsKey(STypeInt.WIELD_REQUIREMENTS_2_INT))
                                    wieldReq2 = app.i_prof._intStatsTable.hashTable[STypeInt.WIELD_REQUIREMENTS_2_INT].ToString();
                                string wieldSkillType2 = "";
                                if (app.i_prof._intStatsTable.hashTable.ContainsKey(STypeInt.WIELD_SKILLTYPE_2_INT))
                                    wieldSkillType2 = app.i_prof._intStatsTable.hashTable[STypeInt.WIELD_SKILLTYPE_2_INT].ToString();
                                string wieldDiff2 = "";
                                if (app.i_prof._intStatsTable.hashTable.ContainsKey(STypeInt.WIELD_DIFFICULTY_2_INT))
                                    wieldDiff2 = app.i_prof._intStatsTable.hashTable[STypeInt.WIELD_DIFFICULTY_2_INT].ToString();

                                string weaponType = "";
                                if (app.i_prof._intStatsTable.hashTable.ContainsKey(STypeInt.WEAPON_TYPE_INT))
                                    weaponType = app.i_prof._intStatsTable.hashTable[STypeInt.WEAPON_TYPE_INT].ToString();

                                string spellSet = "";
                                if (app.i_prof._intStatsTable.hashTable.ContainsKey(STypeInt.EQUIPMENT_SET_ID_INT))
                                    spellSet = app.i_prof._intStatsTable.hashTable[STypeInt.EQUIPMENT_SET_ID_INT].ToString();

                                string description = "";
                                if (app.i_prof._strStatsTable.hashTable.ContainsKey(STypeString.LONG_DESC_STRING))
                                {
                                    description = app.i_prof._strStatsTable.hashTable[STypeString.LONG_DESC_STRING].m_buffer;
                                    description = description.Replace("\"", "\"\""); // escape quotes with...another quote? CSV is weird.
                                }

                                string difficulty = "";
                                if (app.i_prof._intStatsTable.hashTable.ContainsKey(STypeInt.ITEM_DIFFICULTY_INT))
                                    difficulty = app.i_prof._intStatsTable.hashTable[STypeInt.ITEM_DIFFICULTY_INT].ToString();

                                string spellcraft = "";
                                if (app.i_prof._intStatsTable.hashTable.ContainsKey(STypeInt.ITEM_SPELLCRAFT_INT))
                                    spellcraft = app.i_prof._intStatsTable.hashTable[STypeInt.ITEM_SPELLCRAFT_INT].ToString();

                                string maxMana = "";
                                if (app.i_prof._intStatsTable.hashTable.ContainsKey(STypeInt.ITEM_MAX_MANA_INT))
                                    maxMana = app.i_prof._intStatsTable.hashTable[STypeInt.ITEM_MAX_MANA_INT].ToString();

                                string manaCost = "";
                                if (app.i_prof._floatStatsTable.hashTable.ContainsKey(STypeFloat.MANA_RATE_FLOAT))
                                    manaCost = app.i_prof._floatStatsTable.hashTable[STypeFloat.MANA_RATE_FLOAT].ToString();

                                //theFile.WriteLine("container guid, container wcid, container name, loot guid, loot wcid, loot name, item type, weapon type, value, material, workmanship,num tinks, gem count, gem material, spell set, spells");

                                result = thisContentGUID + "," +
                                        containerWCID + "," +
                                        containerName + "," +
                                        containerPos + "," +
                                        thisContentGUID + "," +
                                        lootWCID + "," +
                                        lootName + "," +
                                        itemType + "," +
                                        weaponType + "," +
                                        "\"" + description + "\"," +
                                        value + "," +
                                        materialId + "," +
                                        workmanship + "," +
                                        numTinks + "," +
                                        gemCount + "," +
                                        gemMaterial + "," +

                                        clothingPriority + "," +
                                        locations + "," +

                                        wieldReq + "," +
                                        wieldSkillType + "," +
                                        wieldDiff + "," +

                                        wieldReq2 + "," +
                                        wieldSkillType2 + "," +
                                        wieldDiff2 + "," +

                                        itemLevel + "," +

                                        spellcraft + "," +
                                        difficulty + "," +
                                        maxMana + "," +
                                        manaCost + "," +

                                        spellSet + "," +
                                        spells;
                            }
                        }
                    }
                }
            }

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
