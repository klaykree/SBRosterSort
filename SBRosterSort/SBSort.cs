using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Runtime.InteropServices;
using System.Threading;
using System.IO;

namespace SBRosterSort
{
    public partial class SBSort : Form
    {
        public class Record
        {
            public Record(string Name)
            {
                this.Name = Name;
            }

            public string Name;
            public uint Matches = 0;
            public uint Wins = 0;
            public uint Losses = 0;
        }

        public class SpecificFight
        {
            public SpecificFight(int NameSplitIndex)
            {
                this.NameSplitIndex = NameSplitIndex;
        }

            public int NameSplitIndex;
            public uint Fighter1Wins = 0;
            public uint Fighter2Wins = 0;
        }

        struct FighterPair
        {
            public Record Fighter1;
            public Record Fighter2;
        }

        System.IO.TextReader m_StreamInput;
        System.IO.TextWriter m_StreamOutput;

        Thread m_PollChatThread;

        Dictionary<string, Record> m_Fighters = new Dictionary<string, Record>();
        Dictionary<string, SpecificFight> m_SpecificFights = new Dictionary<string, SpecificFight>();
        
        FighterPair m_CurrentFighters;

        public SBSort()
        {
            InitializeComponent();
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            Connect();

            //Application.Idle += HandleApplicationIdle;

            SaveLoad.FighterSerializeData FighterData = SaveLoad.Load();

            if(FighterData != null)
            {
                m_Fighters = new Dictionary<string, Record>(FighterData.Fighters);
                m_SpecificFights = new Dictionary<string, SpecificFight>(FighterData.SpecificFights);
            }

            m_PollChatThread = new Thread(new ThreadStart(PollChat));
            m_PollChatThread.Start();
        }

        public void PollChat()
        {
            #if DEBUG
            //m_StreamInput = new System.IO.StreamReader("DummySB.txt");
            #endif

            string Buf;

            //Process each line received from irc server
            for(Buf = m_StreamInput.ReadLine() ; ; Buf = m_StreamInput.ReadLine())
            {
                if(Buf == null)
                    return;

                if(Buf == string.Empty)
                    continue;

                //Send pong reply to any ping messages
                if(Buf.StartsWith("PING "))
                {
                    m_StreamOutput.Write(Buf.Replace("PING", "PONG") + "\r\n");
                    m_StreamOutput.Flush();
                }

                if(Buf[0] != ':')
                    continue;

                #if DEBUG
                Console.WriteLine(Buf);
                #endif
                
                //IRC commands come in one of these formats:
                //:NICK!USER@HOST COMMAND ARGS ... :DATA\r\n
                //:SERVER COMAND ARGS ... :DATA\r\n

                //waifu4u: Bets are OPEN for Prinny vs Jean pielle! (B Tier) (matchmaking)
                //waifu4u: Bets are locked. Prinny (2) - $560,087, Jean pielle (3) - $318,174
                //waifu4u: Buront wins! Payouts to Team Red. 23 more matches until the next tournament!
                //waifu4u: ItsBoshyTime Asellus EX has been promoted!
                //waifu4u: ItsBoshyTime Magaki EX2 has been demoted!

                //SaltyBet: Exhibitions will start in 90 seconds! Thanks for watching!
                //SaltyBet: Matchmaking will start in 90 seconds! Thanks for watching!

                string[] InputMessage = Buf.Split(':');
                
                if(InputMessage[1].StartsWith("waifu4u!"))
                {
                    if(InputMessage.Length > 2)
                    {
                        if(InputMessage[2].Contains("Tier) (matchmaking)")) //Bets open message
                        {
                            ParseOpenBetsMessage(InputMessage[2]);
                        }
                        else if(InputMessage[2].Contains("wins! Payouts")) //Fight over message
                        {
                            UpdateCurrentFightersRecords(InputMessage[2]);
                        }
                    }
                }
            }
        }

        void ParseOpenBetsMessage(string a_Message)
        {
            m_CurrentFighters = GetCurrentFighters(a_Message);
        }

        //waifu4u: Bets are OPEN for Prinny vs Jean pielle! (B Tier) (matchmaking)
        FighterPair GetCurrentFighters(string a_Message)
        {
            FighterPair Pair = new FighterPair();

            string RedName = string.Empty;
            string BlueName = string.Empty;

            int RedNameIndex = a_Message.IndexOf("OPEN for");

            if(RedNameIndex != -1)
            {
                int BetweenFighterNamesIndex = a_Message.IndexOf(" vs ");
                RedNameIndex += 9; //"OPEN for " is 9 characters long

                RedName = a_Message.Substring(RedNameIndex, BetweenFighterNamesIndex - RedNameIndex);

                int BlueNameIndex = BetweenFighterNamesIndex + 4; //" vs " is 4 characters long
                int BlueTeamNameEndIndex = a_Message.IndexOf("! (");

                BlueName = a_Message.Substring(BlueNameIndex, BlueTeamNameEndIndex - BlueNameIndex);
            }
            
            if(!m_Fighters.TryGetValue(RedName, out Pair.Fighter1))
            {
                m_Fighters.Add(RedName, new Record(RedName));
                Pair.Fighter1 = m_Fighters[RedName];
            }

            if(!m_Fighters.TryGetValue(BlueName, out Pair.Fighter2))
            {
                m_Fighters.Add(BlueName, new Record(BlueName));
                Pair.Fighter2 = m_Fighters[BlueName];
            }

            return Pair;
        }

        //Buront wins! Payouts to Team Red. 23 more matches until the next tournament!
        void UpdateCurrentFightersRecords(string a_Message)
        {
            if(m_CurrentFighters.Fighter1 == null || m_CurrentFighters.Fighter2 == null)
            {
                return;
            }

            int LexOrder = String.Compare(m_CurrentFighters.Fighter1.Name, m_CurrentFighters.Fighter2.Name);
            Record LexLowerNamed;
            Record LexHigherNamed;
            if(LexOrder <= 0)
            {
                LexLowerNamed = m_CurrentFighters.Fighter1;
                LexHigherNamed = m_CurrentFighters.Fighter2;
            }
            else
            {
                LexLowerNamed = m_CurrentFighters.Fighter2;
                LexHigherNamed = m_CurrentFighters.Fighter1;
            }

            SpecificFight Fight;
            string LexFightName = LexLowerNamed.Name + LexHigherNamed.Name;
            if(!m_SpecificFights.TryGetValue(LexFightName, out Fight))
            {
                Fight = new SpecificFight(LexLowerNamed.Name.Length);
                m_SpecificFights[LexFightName] = Fight;
                Fight = m_SpecificFights[LexFightName];
            }
            
            Record Win = null;
            Record Lose = null;

            //Not a foolproof way to check if this win message followed the fight open message but better than nothing
            if(a_Message.Contains("Payouts to Team Red") && a_Message.Contains(m_CurrentFighters.Fighter1.Name))
            {
                Win = m_CurrentFighters.Fighter1;
                Lose = m_CurrentFighters.Fighter2;
            }
            else if(a_Message.Contains("Payouts to Team Blue") && a_Message.Contains(m_CurrentFighters.Fighter2.Name))
            {
                Win = m_CurrentFighters.Fighter2;
                Lose = m_CurrentFighters.Fighter1;
            }

            #if DEBUG
            if(Win == null || Lose == null)
            {
                Console.WriteLine("UpdateCurrentFightersRecords failed to find who won/lost");
                return;
            }
            #endif

            Win.Matches += 1;
            Win.Wins += 1;
            Lose.Matches += 1;
            Lose.Losses += 1;

            if(LexLowerNamed.Name == Win.Name)
                ++Fight.Fighter1Wins;
            else
                ++Fight.Fighter2Wins;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct NativeMessage
        {
            public IntPtr Handle;
            public uint Message;
            public IntPtr WParameter;
            public IntPtr LParameter;
            public uint Time;
            public Point Location;
        }

        [DllImport("user32.dll")]
        public static extern int PeekMessage(out NativeMessage message, IntPtr window, uint filterMin, uint filterMax, uint remove);

        bool IsApplicationIdle()
        {
            NativeMessage result;
            return PeekMessage(out result, IntPtr.Zero, (uint)0, (uint)0, (uint)0) == 0;
        }

        void HandleApplicationIdle(object sender, EventArgs e)
        {
            while(IsApplicationIdle())
            {
            }
        }

        private void Connect()
        {
            int Port;
            string Buf, Nick, Server, Chan;
            System.Net.Sockets.TcpClient Sock = new System.Net.Sockets.TcpClient();
            System.IO.TextReader Input;
            System.IO.TextWriter Output;

            Nick = "justinfan54";
            Server = "irc.twitch.tv";
            Port = 6667;
            Chan = "#saltybet";

            //Connect to irc server and get input and output text streams from TcpClient
            Sock.Connect(Server, Port);
            if(!Sock.Connected)
            {
                Console.WriteLine("Failed to connect!");
                return;
            }

            Input = new System.IO.StreamReader(Sock.GetStream());
            Output = new System.IO.StreamWriter(Sock.GetStream());

            Output.Write("NICK " + Nick + "\r\n");
            Output.Flush();

            //Process each line received from irc server
            for(Buf = Input.ReadLine() ; ; Buf = Input.ReadLine())
            {
                //Send pong reply to any ping messages
                if(Buf.StartsWith("PING ")) { Output.Write(Buf.Replace("PING", "PONG") + "\r\n"); Output.Flush(); }

                if(Buf[0] != ':') continue;

                //IRC commands come in one of these formats:
                //:NICK!USER@HOST COMMAND ARGS ... :DATA\r\n
                //:SERVER COMAND ARGS ... :DATA\r\n
                
                //After server sends 001 command, we can set mode to bot and join a channel
                if(Buf.Split(' ')[1] == "001")
                {
                    Output.Write(
                        "MODE " + Nick + " +B\r\n" +
                        "JOIN " + Chan + "\r\n"
                    );
                    Output.Flush();
                }

                //Login complete
                if(Buf.Contains(":" + Nick + ".tmi.twitch.tv"))
                {
                    m_StreamInput = Input;
                    m_StreamOutput = Output;
                    return;
                }

                string[] Message = Buf.Split(':');

                if(Message.Length > 2)
                {
                    if(Message[2] == "Error logging in")
                    {
                        return;
                    }
                }
            }
        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            m_PollChatThread.Abort();
        }

        private void BtnSave_Click(object sender, EventArgs e)
        {
            SaveLoad.FighterSerializeData Data = new SaveLoad.FighterSerializeData();
            Data.Fighters = m_Fighters;
            Data.SpecificFights = m_SpecificFights;
            SaveLoad.Save(Data);
        }
    }
}