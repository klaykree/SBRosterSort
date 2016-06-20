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

namespace SBRosterSort
{
    public partial class Form1 : Form
    {
        class Record
        {
            public Record(string Name)
            {
                this.Name = Name;
            }

            public string Name;
            public uint Matches = 0;
            public uint Wins = 0;
            public uint Losses = 0;
            public Dictionary<string, MinimalRecord> SpecificFights = new Dictionary<string, MinimalRecord>();
        }

        class MinimalRecord
        {
            public uint Matches = 0;
            public uint Wins = 0;
            public uint Losses = 0;
        }

        struct FighterPair
        {
            public Record Left;
            public Record Right;
        }

        System.IO.TextReader m_StreamInput;
        System.IO.TextWriter m_StreamOutput;

        Thread m_PollChatThread;

        Dictionary<string, Record> m_XTier;
        Dictionary<string, Record> m_STier;
        Dictionary<string, Record> m_ATier;
        Dictionary<string, Record> m_BTier;

        Dictionary<string, Record> m_CurrentTier;
        FighterPair m_CurrentFighters;

        public Form1()
        {
            InitializeComponent();

            m_XTier = new Dictionary<string, Record>();
            m_STier = new Dictionary<string, Record>();
            m_ATier = new Dictionary<string, Record>();
            m_BTier = new Dictionary<string, Record>();

            Connect();
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            Application.Idle += HandleApplicationIdle;

            m_PollChatThread = new Thread(new ThreadStart(PollChat));
            m_PollChatThread.Start();
        }

        public void PollChat()
        {
            string Buf;

            //Process each line received from irc server
            for(Buf = m_StreamInput.ReadLine() ; ; Buf = m_StreamInput.ReadLine())
            {
                //Send pong reply to any ping messages
                if(Buf.StartsWith("PING "))
                {
                    m_StreamOutput.Write(Buf.Replace("PING", "PONG") + "\r\n");
                    m_StreamOutput.Flush();
                }

                if(Buf[0] != ':') continue;

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
            m_CurrentTier = GetCurrentTier(a_Message);
            m_CurrentFighters = GetCurrentFighters(a_Message);
        }

        Dictionary<string, Record> GetCurrentTier(string a_Message)
        {
            int MatchmakingIndex = a_Message.IndexOf("Tier) (matchmaking)");

            if(MatchmakingIndex != -1)
            {
                if(a_Message[MatchmakingIndex - 2] == 'X')
                {
                    return m_XTier;
                }

                if(a_Message[MatchmakingIndex - 2] == 'S')
                {
                    return m_STier;
                }

                if(a_Message[MatchmakingIndex - 2] == 'A')
                {
                    return m_ATier;
                }

                else if(a_Message[MatchmakingIndex - 2] == 'B')
                {
                    return m_BTier;
                }
            }

            return null;
        }

        //waifu4u: Bets are OPEN for Prinny vs Jean pielle! (B Tier) (matchmaking)
        FighterPair GetCurrentFighters(string a_Message)
        {
            FighterPair Pair = new FighterPair();

            string LeftName = string.Empty;
            string RightName = string.Empty;

            int RedNameIndex = a_Message.IndexOf("OPEN for");

            if(RedNameIndex != -1)
            {
                int BetweenFighterNamesIndex = a_Message.IndexOf(" vs ");
                RedNameIndex += 9;

                LeftName = a_Message.Substring(RedNameIndex, BetweenFighterNamesIndex - RedNameIndex);

                int BlueNameIndex = BetweenFighterNamesIndex + 4;
                int BlueTeamNameEndIndex = a_Message.IndexOf("! (");

                RightName = a_Message.Substring(BlueNameIndex, BlueTeamNameEndIndex - BlueNameIndex);
            }
            
            if(!m_CurrentTier.TryGetValue(LeftName, out Pair.Left))
            {
                m_CurrentTier.Add(LeftName, new Record(LeftName));
                Pair.Left = m_CurrentTier[LeftName];
            }

            if(!m_CurrentTier.TryGetValue(RightName, out Pair.Right))
            {
                m_CurrentTier.Add(RightName, new Record(RightName));
                Pair.Right = m_CurrentTier[RightName];
            }

            return Pair;
        }

        //Buront wins! Payouts to Team Red. 23 more matches until the next tournament!
        void UpdateCurrentFightersRecords(string a_Message)
        {
            if(m_CurrentFighters.Left == null || m_CurrentFighters.Right == null)
            {
                return;
            }

            Record Win = null;
            Record Lose = null;

            if(a_Message.Contains("Payouts to Team Red"))
            {
                Win = m_CurrentFighters.Left;
                Lose = m_CurrentFighters.Right;
            }
            else if(a_Message.Contains("Payouts to Team Blue"))
            {
                Win = m_CurrentFighters.Right;
                Lose = m_CurrentFighters.Left;
            }

            if(Win == null || Lose == null)
            {
                Console.WriteLine("UpdateCurrentFightersRecords failed to find who won/lost");
            }

            Win.Matches += 1;
            Win.Wins += 1;
            Lose.Matches += 1;
            Lose.Losses += 1;

            MinimalRecord LostOpponent = null;
            if(!Win.SpecificFights.TryGetValue(Lose.Name, out LostOpponent))
            {
                Win.SpecificFights.Add(Lose.Name, new MinimalRecord());
                LostOpponent = Win.SpecificFights[Lose.Name];
            }

            LostOpponent.Matches += 1;
            LostOpponent.Losses += 1;

            MinimalRecord WonOpponent = null;
            if(!Lose.SpecificFights.TryGetValue(Lose.Name, out WonOpponent))
            {
                Lose.SpecificFights.Add(Win.Name, new MinimalRecord());
                WonOpponent = Lose.SpecificFights[Win.Name];
            }

            WonOpponent.Matches += 1;
            WonOpponent.Wins += 1;
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

            //Connect to irc server and get input and output text streams from TcpClient.
            Sock.Connect(Server, Port);
            if(!Sock.Connected)
            {
                Console.WriteLine("Failed to connect!");
                return;
            }
            Input = new System.IO.StreamReader(Sock.GetStream());
            Output = new System.IO.StreamWriter(Sock.GetStream());

            Output.Write(
                "NICK " + Nick + "\r\n"
            );
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
    }
}
