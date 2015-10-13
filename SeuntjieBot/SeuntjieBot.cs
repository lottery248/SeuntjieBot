﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Data.SqlClient;
using System.Security.Cryptography;
using System.Diagnostics;
using System.Threading;
using System.Reflection;
using System.Net;
using System.Runtime.Serialization.Json;

namespace SeuntjieBot
{

    /// <summary>
    /// Core Class that processes and determines the responses of chat commands
    /// </summary>
    public class SeuntjieBot
    {
        
        /// <summary>
        /// Deserializes a json string into an object of type T
        /// </summary>
        /// <typeparam name="T">Type of object to be parsed to</typeparam>
        /// <param name="jsonString">Json String to convert from</param>
        /// <returns>Deserialized object</returns>
        public static T JsonDeserialize<T>(string jsonString)
        {

            DataContractJsonSerializer ser = new DataContractJsonSerializer(typeof(T));
            MemoryStream ms = new MemoryStream(Encoding.UTF8.GetBytes(jsonString));
            T obj = (T)ser.ReadObject(ms);
            return obj;
        }

        /// <summary>
        /// Seerializes an object of type T into a json string
        /// </summary>
        /// <typeparam name="T">Type of object to be parsed</typeparam>
        /// <param name="t">Object to convert to json string</param>
        /// <returns>Serialized json string</returns>
        public static string JsonSerializer<T>(T t)
        {
            DataContractJsonSerializer ser = new DataContractJsonSerializer(typeof(T));
            MemoryStream ms = new MemoryStream();
            ser.WriteObject(ms, t);
            string jsonString = Encoding.UTF8.GetString(ms.ToArray());
            ms.Close();
            return jsonString;
        }

        //delegates
        /// <summary>
        /// Delegate for SendMessage Event
        /// </summary>
        /// <param name="Message">Object containing the message and user information for private messaging</param>
        public delegate void dSendMessage(SendMessage Message);

        /// <summary>
        /// Delegegate for te GetBalance Event, to fetch the current balance of the bot
        /// </summary>
        /// <returns>Current balance of the bot</returns>
        public delegate double dGetBalance();

        /// <summary>
        /// Delegate for Rain event, for when the bot needs to send rain to a user
        /// </summary>
        /// <param name="RainOn">User object to rain on. Should contain any info needed to perform the rain</param>
        /// <param name="Amount">Amount to rain on the user</param>
        /// <returns>Wether rain succeeded or not</returns>
        public delegate bool dRain(User RainOn, double Amount);

        /// <summary>
        /// Delegate for ActiveUsersChanged event. Triggers if there are significant changes in the active users list
        /// </summary>
        /// <param name="ActiveUsers">List of active and eligible users</param>
        public delegate void dUpdateActive(User[] ActiveUsers);
        
        //events
        /// <summary>
        /// Triggers when SeuntjieBot has to send a chat message
        /// </summary>
        public event dSendMessage SendMessage;

        /// <summary>
        /// Triggers when Seuntjiebot needs the balance of the current chatting account
        /// </summary>
        public event dGetBalance GetBalance;

        /// <summary>
        /// triggers when SeuntjieBot is trying to send a tip/Rain on a user
        /// </summary>
        public event dRain SendRain;

        /// <summary>
        /// Triggers whenever a new user is added/removed to/from the active list or a usres state is changed to/from eligible.
        /// </summary>
        public event dUpdateActive ActiveUsersChanged;


        //properties
        /// <summary>
        /// list of regular expressions used to determine whether an address is valid or not
        /// </summary>
        public string[] AddressExpressions { get; set; }

        /// <summary>
        /// sets the bot to supress all output and write only to the log when it should respond.
        /// </summary>
        public bool LogOnly { get; set; }

        /// <summary>
        /// Determines whether raining on users is enabled or not
        /// </summary>
        public bool RainEnabled { get; set; }

        /// <summary>
        /// The percentage of the available balance the bot will rain when it rains, if larger than the MinRain value
        /// </summary>
        public decimal RainPercentage { get; set; }

        /// <summary>
        /// The minimum rain amount that can/may be sent
        /// </summary>
        public decimal MinRain { get; set; }

        /// <summary>
        /// How often the bot should rain on users
        /// </summary>
        public TimeSpan RainINterval { get; set; }

        
        

        //variables
        List<User> activeusers = new List<User>();
        Random r = new Random();
        public List<Command> Commands = new List<Command>();
        Timer tmrRain;
        Timer tmrUsers;
        Timer tmrSend;
        Timer tmrCurrency;
        Queue<SendMessage> MessageQueue = new Queue<SendMessage>();
        DateTime LastSent = DateTime.Now;
        Dictionary<string, bool> CommandState = new Dictionary<string, bool>();


        //constructors
        public SeuntjieBot()
        {
            LogOnly = false;
            Initialize();
        }
        public SeuntjieBot(bool LogOnly)
        {
            this.LogOnly = LogOnly;
            Initialize();
        }

        /// <summary>
        /// Initializes the internal variables and timers for seuntjiebot.
        /// </summary>
        private void Initialize()
        {
            tmrRain = new Timer(tmrRainTick, null, 100,100);
            tmrUsers = new Timer(tmrUsersTick, null, 1000, 1000);
            tmrSend = new Timer(tmrSendTick, null, 10, 10);
            tmrCurrency = new Timer(tmrCurrencyTick, null, 30000, 30000);
            LastSent = DateTime.Now;
            RainINterval = new TimeSpan(0, 10, 0);
            loadCommands();
            gettotalRains();
            CommandState.Add("balance", true);
            CommandState.Add("user", true);
            CommandState.Add("redlist", true);
            CommandState.Add("blacklist", true);
            CommandState.Add("status", true);
            CommandState.Add("title", true);
            CommandState.Add("note", true);
            CommandState.Add("usertype", true);
            CommandState.Add("halfmute", true);
            CommandState.Add("btc", true);
            CommandState.Add("convert", true);
            CommandState.Add("mute", true);
            CommandState.Add("unmute", true);
            CommandState.Add("rained", true);
            CommandState.Add("rain", true);
            CommandState.Add("last", true);
            CommandState.Add("bot", true);
        }

        //core functions

        /// <summary>
        /// Loads the commands from the commands.txt file, if it exists
        /// </summary>
        public void loadCommands()
        {
            Commands = new List<Command>();
            if (File.Exists("Commands.txt"))
            {
                using (StreamReader sr = new StreamReader("commands.txt"))
                {
                    while (!sr.EndOfStream)
                    {
                        string[] tmp = sr.ReadLine().Split('|');
                        Commands.Add(new Command(tmp[0], tmp[1], tmp[2]));
                    }
                }
            }
        }

        /// <summary>
        /// Saves the commands to the Commands.txt file
        /// </summary>
        public void SaveCommands()
        {
            string s = "";
            foreach (Command c in Commands)
            {
                s += c.sCommand + "|" + c.Response + "|" + (c.Enabled ? "1" : "0") + "\n";
            }
            File.WriteAllText("commands.txt", s);

        }

        void InternalSendMessage(SendMessage Message)
        {
            writelog(Message.Message, Message.ToUser);
            if (!LogOnly && SendMessage != null)
                SendMessage(Message);
        }

        /// <summary>
        /// responds to mods and admins only
        /// </summary>
        bool bhalfmute = false;

        #region Process Commands

        /// <summary>
        /// receive message handler for asyncronous clients
        /// </summary>
        /// <param name="chat"></param>
        public void ReceiveMessage(object chat)
        {
            ReceiveMessage(chat as chat);
        }

        /// <summary>
        /// Processes chat messages and will trigger responses as required. Main logic of the bot happens here
        /// </summary>
        /// <param name="chat">Chat object containing the message and some user info of the initiating user</param>
        public void ReceiveMessage(chat chat)
        {
            chat.insert();
            User CurUser = User.FindUser((int)chat.UID);
            if (CurUser == null)
            {
                CurUser = User.FindUser(chat.User);
                if (CurUser == null)
                return;
            }
            if (activeusers.Contains(CurUser))
            {
                CurUser = activeusers[activeusers.IndexOf(CurUser)];
            }
            bool ismod = CurUser.UserType.ToLower() == "admin" || 
                CurUser.UserType.ToLower() == "mod" || 
                CurUser.UserType.ToLower() == "dev" || 
                CurUser.UserType.ToLower() == "op";
            bool pm = chat.Type == "pm";
            bool warned = checkwarning(CurUser);
            bool issued = false;
            bool sent = false;
            bool comm = false;
            if (CurUser.UserType.ToLower() == "scam" && !warned)
            {
                // pm + string.Format(" λ WARNING! {0} is a suspected scammer! λ ", CurUser.Username)
                InternalSendMessage(new SendMessage { Message=string.Format(" λ WARNING! {0} is a suspected scammer! λ ", CurUser.Username), Pm=false, ToUser=CurUser });
                string[] tmp = { (chat.User), DateTime.Now.ToString() };
                issued = true;
                CurUser.Warning = DateTime.Now;
            }
            if (CurUser.Listed < 2)
            {
                long tooquick = 0;
                
                List<string> msgs = chat.Message.Split(' ').ToList<string>();
                tooquick = CurUser.getCommandscore();
                bool nots = false;
                if (msgs[0].ToLower().StartsWith("!") && msgs[0].ToLower() != "!s")
                {
                    nots = true;
                    msgs.Insert(1, msgs[0].Substring(1));
                    msgs[0] = "!s";
                }
                if (msgs[0].ToLower() == "!s")
                {
                    if (tooquick >= 2)
                    {
                        writelog("Too Quick", CurUser);
                        InternalSendMessage(new SendMessage { Message = "Please wait a minute before making another request", Pm = true, ToUser = CurUser });
                    }

                    else if (tooquick < 2 && msgs.Count >= 2)
                    {
                        msgs[1] = msgs[1].ToLower();
                        foreach (Command curom in Commands)
                        {
                            if (msgs[1].ToLower() == (curom.sCommand.ToLower()) && curom.Enabled)
                            {
                                if (!LogOnly)
                                    InternalSendMessage(new SendMessage { Message = curom.Response, ToUser = CurUser, Pm = pm });
                                sent = true;
                                break;
                            }
                        }
                        if (!sent)
                        {
                            string Msg = GetCurrency(msgs[1]);
                            if (Msg != "-1")
                            {
                                if (true)
                                {
                                    sent = true;
                                    InternalSendMessage(new SendMessage { Message = Msg, Pm = pm, ToUser = CurUser });
                                }
                                else
                                {
                                    InternalSendMessage(new SendMessage { Message = Msg, Pm = true, ToUser = CurUser });
                                    sent = true;
                                }
                            }
                            if (!sent)
                            {
                                try
                                {
                                    Type thisType = this.GetType();
                                    MethodInfo[] meths = thisType.GetMethods();
                                    MethodInfo theMethod = thisType.GetMethod(msgs[1]);
                                    object[] Params = new object[] { chat, msgs, ismod, pm, CurUser };
                                    if (theMethod != null)
                                    {
                                        sent = (bool)theMethod.Invoke(this, Params);
                                    }
                                }
                                catch
                                {

                                }
                            }
                            if (!sent && tooquick < 2 && !nots)
                            {
                                comm = true;
                                sent = true;
                                InternalSendMessage(new SendMessage { Message = CommandString(), ToUser = CurUser, Pm = true });
                            }
                            if (!sent && tooquick < 2)
                            {
                                if (!sent)
                                    sent = CheckBotCommands(sent, chat, CurUser);

                            }
                        }


                    }
                }
            }

            if (sent && CurUser.UserType.ToLower()!="op")
            {
                CurUser.CommandScore.Add(new score ((comm||pm?1:2),DateTime.Now));
            
                
            }
            //update last active time and score for user
            
            
            if (CurUser.Listed<1)
            {
                int score = (pm||chat.Message.Length<=2?0: (chat.Message.Length < 50) ? 1 : (chat.Message.Length < 100) ? 2 : 3);
                updateactivetime(CurUser, score);
            }
        }

        /// <summary>
        /// Checks whether a non ! command should be executed, such as fuck you bot.
        /// </summary>
        /// <param name="sent"></param>
        /// <param name="chat"></param>
        /// <param name="CurUser"></param>
        /// <returns></returns>
        private bool CheckBotCommands(bool sent, chat chat, User CurUser)
        {
            List<KeyValuePair<string, int>> Positives = new List<KeyValuePair<string, int>>();
            List<KeyValuePair<string, int>> Negatives = new List<KeyValuePair<string, int>>();
            List<string> Specifics = new List<string>();
            string[] Msgs = chat.Message.Split(' ');
            if (Msgs[Msgs.Length - 1] == ("bot") || Msgs[Msgs.Length - 1] == ("seuntjiebot"))
            {
                if (chat.Message.EndsWith("you bot")||chat.Message.ToLower().EndsWith("you seuntjiebot"))
                {
                    int score = 0;
                    foreach (KeyValuePair<string,int> kv in Positives)
                    {
                        if (chat.Message.ToLower().Contains(kv.Key))
                        {
                            score += kv.Value;
                        }
                    }
                }
                if (chat.Message.EndsWith("you bot") || chat.Message.ToLower().EndsWith("you seuntjiebot"))
                {
                    int score = 0;
                    foreach (KeyValuePair<string, int> kv in Negatives)
                    {
                        if (chat.Message.ToLower().Contains(kv.Key))
                        {
                            score -= kv.Value;
                        }
                    }
                }
                
            }
            return sent;
        }
        
        /// <summary>
        /// Generates a List of possible commands that are enabled in the bot
        /// </summary>
        /// <returns>String message containting list of enabled commands</returns>
        string CommandString()
        {

            string s = "Commands: ";
            bool listed = false;
            //if (parent.chkUser.Checked)
            {
                s += "User [username]";
                listed = true;
            }
            //if (parent.chkBalance.Checked)
            {
                if (listed)
                    s += ", ";
                s += "Balance";
                listed = true;
            }
            //if (parent.chkChart.Checked)
            {
                if (listed)
                    s += ", ";
                s += "Chart (username)";
                listed = true;
            }
            //if (parent.chkAddress.Checked)
            {
                if (listed)
                    s += ", ";
                s += "address [bitcoin address]";
                listed = true;
            }
            //if (parent.chkPrice.Checked)
            {
                if (listed)
                    s += ", ";
                s += "[Currency]";
                listed = true;
            }
            //if (parent.chkRained.Checked)
            {
                if (listed)
                    s += ", ";
                s += "Rained";
                listed = true;
            }
            //if (parent.chkVerify.Checked)
            {
                if (listed)
                    s += ", ";
                s += "verify [betid/username]";
                listed = true;
            }

            foreach (Command s2 in Commands)
            {

                if (s2.Enabled)
                {
                    if (listed)
                        s += ", ";
                    s += s2.sCommand;
                }
            }
            return s;
        }

        public bool balance(chat chat, List<string> msgs, bool ismod, bool pm, User CurUser)
        {
            if (CommandState["balance"])
            {
                try
                {
                    if (GetBalance != null)
                    {
                        decimal bal = (decimal)GetBalance();
                        decimal avgTime = 10m;//getAvgTime();
                        decimal fraction = (bal / RainPercentage);
                        decimal intervals = (decimal)(Math.Log((double)fraction) / Math.Log((double)(1m / 0.999m)));
                        intervals = Math.Ceiling(intervals);
                        intervals += (0.1m / 0.0001m);

                        if (bal < 0.1m)
                            intervals = Math.Floor(bal / MinRain);

                        int minutes = (int)(intervals * avgTime);
                        TimeSpan TimeLeft = new TimeSpan(0, minutes, 0);

                        InternalSendMessage(new SendMessage { Message = string.Format("Rainjar balance: {3:0.00000000}. Approximately {0} days, {1} hours and {2} minutes", TimeLeft.Days, TimeLeft.Hours, TimeLeft.Minutes, bal), Pm = pm, ToUser = CurUser });
                        return true;
                    }
                    else
                    {
                        return false;
                    }

                }
                catch (Exception e)
                {
                    dumperror(e.Message);
                    return false;
                }
            }
            return false;
        }
        public bool user(chat chat, List<string> msgs, bool ismod, bool pm, User CurUser)
        {
            if (CommandState["user"])
            {
                string username = "";
                GetName(msgs, 2, out username);

                User tmp = null;

                long id = 0;
                if (long.TryParse(username, out id))
                {
                    tmp = User.FindUser((int)id);
                }
                else
                {
                    tmp = User.FindUser(username);
                }

                
                if (tmp != null)
                {
                    //(parent is JD)? (long.TryParse(user, out tmpid))?user.ToString(): User.FindUser(user).id.ToString() :user

                    if (tmp == null)
                    {
                        writelog("requested user: " + username + ". User not found", CurUser); 
                        InternalSendMessage(new SendMessage { Message = "User not found.", ToUser = CurUser, Pm = pm });
                    }
                    else
                    {
                        string title = tmp.Title;
                        if (title != "")
                        {
                            title = "'" + title + "'";
                        }
                        string message = "";

                        message = string.Format("UID: {6}. Name: {0} {1}, Address: {2}. Times rained: {4}, total received: {5:0.00000000}. {3}",
                            title,
                            tmp.Username,
                            (!string.IsNullOrWhiteSpace(tmp.Address) ? tmp.Address : "NA"),
                            ((tmp.UserType != "user") ? tmp.UserType : ""),
                            tmp.times == "" ? "0" : tmp.times,
                                tmp.rain == "" ? "0" : tmp.rain,
                                tmp.Uid);
                        writelog("requested user: " + username, CurUser);
                        message = " λ " + message + " λ ";
                        InternalSendMessage(new SendMessage { Message = message, ToUser = CurUser, Pm = username.ToLower() != CurUser.Username.ToLower() ? pm : true });

                        return true;
                    }
                }
            }
            return false;
        }
        public bool redlist(chat chat, List<string> msgs, bool ismod, bool pm, User CurUser)
        {
            if (CommandState["redlist"] && ismod)
            {
                string username = "";
                int start = GetName(msgs, 2, out username);
                string reason = BuildString(msgs, start);
                writelog("Redlisted user " + username + " for reason: " + reason, CurUser);

                User tmp = null;
                int id = -1;
                if (int.TryParse(username, out id))
                {
                    tmp = User.FindUser(id);
                    if (tmp == null)
                        tmp = User.FindUser(username);
                }
                else
                {
                    tmp = User.FindUser(username);
                }
                if (tmp!=null)
                {
                    if (tmp.Listed < 1)
                        tmp.Listed = 1;
                    if (activeusers.Contains(tmp))
                    {
                        activeusers[activeusers.IndexOf(tmp)] = tmp;
                    }
                    MSSQL.Instance().Redlist(tmp, reason);


                    InternalSendMessage(new SendMessage { Message = string.Format("{0} has been redlisted for {1}", tmp.Username, reason), Pm = pm, ToUser = CurUser });
                    return true;
                }
                
            }
            return false;
        }
        public bool blacklist(chat chat, List<string> msgs, bool ismod, bool pm, User CurUser)
        {
            if (CommandState["blacklist"] && ismod)
            {
                string username = "";
                int start = GetName(msgs, 2, out username);
                string reason = BuildString(msgs, start);
                writelog("Blacklisted user " + username + " for reason: " + reason, CurUser);

                User tmp = null;
                int id = -1;
                if (int.TryParse(username, out id))
                {
                    tmp = User.FindUser(id);
                    if (tmp == null)
                        tmp = User.FindUser(username);
                }
                else
                {
                    tmp = User.FindUser(username);
                }
                if (tmp != null)
                {
                    if (tmp.Listed < 2)
                        tmp.Listed = 2;
                    if (activeusers.Contains(tmp))
                    {
                        activeusers[activeusers.IndexOf(tmp)] = tmp;
                    }
                    //update to DB
                    MSSQL.Instance().BlackList(tmp, reason);

                    InternalSendMessage(new SendMessage { Message = string.Format("{0} has been blacklisted for {1}", tmp.Username, reason), Pm = pm, ToUser = CurUser });
                    return true;
                }

            }
            return false;
        }
        public bool status(chat chat, List<string> msgs, bool ismod, bool pm, User CurUser)
        {
            if (CommandState["status"] && ismod)
            {
                writelog("Checked status for " + msgs[2], CurUser);
                string username = "";
                GetName(msgs, 2, out username);
                User tmp = null;
                int id = -1;
                if (int.TryParse(username, out id))
                {
                    tmp = User.FindUser(id);
                    if (tmp == null)
                        tmp = User.FindUser(username);
                }
                else
                {
                    tmp = User.FindUser(username);
                }
                if (tmp != null)
                {
                    string Message = "";
                    switch (tmp.Listed)
                    {
                        case 0: Message = "Safe"; break;
                        case 1: Message = "Redlisted: " + tmp.GetRedReason() + ". "; break;
                        case 2: Message = "Blacklisted: " + tmp.GetBlackReason() + ". "; break;
                    }

                    InternalSendMessage(new SendMessage { Message = Message, Pm = pm, ToUser = CurUser });
                    return true;
                }
            }
            return false;
        }
        public bool address(chat chat, List<string> msgs, bool ismod, bool pm, User CurUser)
        {
            if (CommandState["address"])
            {
                if (msgs.Count>2)
                {
                    writelog("updated bitcoin address to " + msgs[2], CurUser);
                    System.Text.RegularExpressions.Regex txt = null;
                    bool valid = false;

                    foreach (string s in AddressExpressions)
                    {

                        //@"^[13][a-km-zA-HJ-NP-Z0-9]{26,33}$"
                        txt = new System.Text.RegularExpressions.Regex(s);
                        valid = txt.IsMatch(msgs[2]);
                    }
                    if (valid)
                    {

                        CurUser.Address = msgs[2];
                        CurUser.updateuser();
                        InternalSendMessage(new SendMessage { Message = "@" + CurUser.Username + " Updated", Pm = pm, ToUser = CurUser });
                    }
                    else
                    {
                        InternalSendMessage(new SendMessage { Message = "@" + CurUser.Username + " Invalid address. Try again in a minute", Pm = pm, ToUser = CurUser });
                    }
                    return true;           
                }
            }
            return false;
        }
        public bool title(chat chat, List<string> msgs, bool ismod, bool pm, User CurUser)
        {
            if (CommandState["title"] && msgs.Count > 3 && ismod)
            {
                string username = "";
                int start = GetName(msgs, 2, out username);
                string title = BuildString(msgs, start);
                User tmp = null;
                int id = -1;
                if (int.TryParse(username, out id))
                {
                    tmp = User.FindUser(id);
                    if (tmp == null)
                        tmp = User.FindUser(username);
                }
                else
                {
                    tmp = User.FindUser(username);
                }
                if (tmp != null)
                {
                    tmp.Title = title;
                    tmp.updateuser();
                    InternalSendMessage(new SendMessage { Message="@"+tmp.Username+" updated", Pm=pm, ToUser=CurUser });
                    return true;
                }
            }
            return false;
        }
        public bool note(chat chat, List<string> msgs, bool ismod, bool pm, User CurUser)
        {
            if (CommandState["note"] && msgs.Count > 3 && ismod)
            {
                string username = "";
                int start = GetName(msgs, 2, out username);
                string note = BuildString(msgs, start);
                User tmp = null;
                int id = -1;
                if (int.TryParse(username, out id))
                {
                    tmp = User.FindUser(id);
                    if (tmp == null)
                        tmp = User.FindUser(username);
                }
                else
                {
                    tmp = User.FindUser(username);
                }
                if (tmp != null)
                {
                    tmp.Note = note;
                    tmp.updateuser();
                    InternalSendMessage(new SendMessage { Message = "@" + tmp.Username + " updated", Pm = pm, ToUser = CurUser });
                    return true;
                }           
            }
            return false;
        }
        public bool usertype(chat chat, List<string> msgs, bool ismod, bool pm, User CurUser)
        {
            if (CommandState["usertype"] && msgs.Count > 3 && ismod)
            {
                bool go = false;
                string username = "";
                int start = GetName(msgs, 2, out username);
                string note = BuildString(msgs, start);

                if (CurUser.UserType == "op" || CurUser.UserType == "admin" || (ismod && note.ToLower() == "scam"))
                    go = true;
                if (go)
                {
                    User tmp = null;
                    int id = -1;
                    if (int.TryParse(username, out id))
                    {
                        tmp = User.FindUser(id);
                        if (tmp == null)
                            tmp = User.FindUser(username);
                    }
                    else
                    {
                        tmp = User.FindUser(username);
                    }
                    if (tmp != null)
                    {
                        tmp.UserType = note;
                        tmp.updateuser();
                        InternalSendMessage(new SendMessage { Message = "@" + tmp.Username + " updated", Pm = pm, ToUser = CurUser });
                        return true;
                    }   
                }
            }
            return false;
        }
        public bool halfmute(chat chat, List<string> msgs, bool ismod, bool pm, User CurUser)
        {
            if (CommandState["halfmute"] && ismod)
            {
                if (ismod)
                {
                    bhalfmute = true;
                    InternalSendMessage(new SendMessage { Message = "Bot will now only respond to mods.", Pm = false, ToUser = CurUser });
                    return true;
                }
            }
            return false;
        }
        public bool btc(chat chat, List<string> msgs, bool ismod, bool pm, User CurUser)
        {
            if (CommandState["btc"])
            {
                InternalSendMessage(new SendMessage { Message = "BTC/USD last trade price according to bitcoinaverage.com: " + mbtc.ToString("0.00"), Pm = pm, ToUser = CurUser });
                return true;
            }
            return false;
        }
        public bool convert(chat chat, List<string> msgs, bool ismod, bool pm, User CurUser)
        {
            if (CommandState["convert"])
            {
                if (msgs.Count >= 5)
                {
                    string from = "";
                    string to = "";
                    string amount = "";
                    int i = GetName(msgs, 2, out from);
                    i = GetName(msgs, i, out to);
                    i = GetName(msgs, i, out amount);
                    double iamount = 0;
                    if (double.TryParse(amount, out iamount))
                    {
                        string s = Convert(from, to, iamount);
                        InternalSendMessage(new SendMessage { Message = s, ToUser = CurUser, Pm = pm });
                        return true;
                    }
                }
                else if (msgs.Count >= 4)
                {
                    string from = "";
                    string to = "";

                    int i = GetName(msgs, 2, out from);
                    i = GetName(msgs, i, out to);
                    string s = Convert(from, to, 1);
                    InternalSendMessage(new SendMessage { Message = s, ToUser = CurUser, Pm = pm });
                        
                    return true;

                }


                InternalSendMessage(new SendMessage { Message = "Convert help: !s convert [from currency] [to currency] (amount)   []=required, ()=optional", Pm = pm, ToUser = CurUser });
                return true;
                
            }
            return false;
        }
        public bool cv(chat chat, List<string> msgs, bool ismod, bool pm, User CurUser)
        {
            return convert(chat, msgs, ismod, pm, CurUser);
        }
        public bool mute(chat chat, List<string> msgs, bool ismod, bool pm, User CurUser)
        {
            if (CommandState["mute"]  && ismod)
            {
                if (CurUser.UserType == "admin" || CurUser.UserType == "op")
                    LogOnly = true;
            }
            return false;
        }
        public bool unmute(chat chat, List<string> msgs, bool ismod, bool pm, User CurUser)
        {
            if (CommandState["unmute"]  && ismod)
            {
                if (CurUser.UserType == "admin" || CurUser.UserType.ToLower() == "op")
                    LogOnly= false;
                if (ismod)
                {
                    bhalfmute = false;
                    InternalSendMessage(new SendMessage { Message = "Bot responding to all.", Pm = false, ToUser = CurUser });
                    return true;
                }
            }
            return false;
        }
        public bool rained(chat chat, List<string> msgs, bool ismod, bool pm, User CurUser)
        {
            if (CommandState["rained"])
            {
                InternalSendMessage(new SendMessage  { Message=string.Format("Times rained: {0}, Total Rained: {1:0.00000000}"), ToUser=CurUser, Pm=pm });
                return true;
            }
            return false;
        }
        public bool rain(chat chat, List<string> msgs, bool ismod, bool pm, User CurUser)
        {
            if (CommandState["rain"] && ismod)
            {
                if (CurUser.UserType == "admin" || CurUser.UserType.ToLower() == "op")
                {
                    Rain(true);
                    return true;
                }
            }
            return false;
        }
        public bool last(chat chat, List<string> msgs, bool ismod, bool pm, User CurUser)
        {
            if (CommandState["last"])
            {
                string user = "";
                int start = GetName(msgs, 2, out user);
                int ID = -1;
                User tmp = null;
                if (int.TryParse(user, out ID))
                {
                    tmp = User.FindUser(ID);
                }
                else
                {
                    tmp = User.FindUser(user);
                }
                if (tmp != null)
                {
                    DateTime tmpDate = tmp.LastSeen;
                    bool seen = false;
                    

                    if (tmp.Uid == CurUser.Uid)
                    {
                        InternalSendMessage(new SendMessage { Message = "You're very forgetfull aren't you " + user + "?", Pm = pm, ToUser = CurUser });
                        return true;
                    }
                    else
                    {
                        if (seen)
                        {
                            TimeSpan ago = DateTime.UtcNow - tmpDate;
                            string s = "";
                            if (ago.TotalMinutes <= 5)
                            {
                                s = string.Format("U:{0} seems to be here now, maybe you're just going a bit blind.", user);
                            }
                            else
                            {
                                s = "u:" + user + " was last seen " + (ago.TotalDays >= 1 ? ago.Days + " Days, " : "") + (ago.TotalHours > 0 ? ago.Hours + " hours, " : "") + (ago.TotalMinutes > 0 ? ago.Minutes + " Minutes " : "") + "ago, at " + tmpDate.ToString("yyyy/MM/dd HH:mm") + " UTC";
                                //s = string.Format("U:{0} was last seen {2} hours and {3} minutes ago at {1} GMT", user, tmpDate, (int)ago.TotalHours, ago.Minutes);
                            }
                            InternalSendMessage(new SendMessage { Message = s, Pm = pm, ToUser = CurUser });
                            return true;
                        }
                        else
                        {
                            InternalSendMessage(new SendMessage { Message = string.Format("I haven't seen U:{0} yet", user), Pm = pm, ToUser = CurUser });
                            return true;
                        }
                    }

                }
                else
                {
                    InternalSendMessage(new SendMessage { Message = string.Format("I haven't seen U:{0} yet", user), Pm = pm, ToUser = CurUser });
                    return true;
                }
                
            }
            return false;
        }
        public bool seen(chat chat, List<string> msgs, bool ismod, bool pm, User CurUser)
        {
            return last(chat, msgs, ismod, pm, CurUser);
        }
        public bool enable(chat chat, List<string> msgs, bool ismod, bool pm, User CurUser)
        {
            if (CommandState["enable"] && msgs.Count > 3 && ismod)
            {
                if (CurUser.UserType=="op")
                {
                    string username = "";
                    GetName(msgs, 2, out username);

                    
                    foreach (Command c in Commands)
                    {
                        if (username == c.sCommand)
                        {
                            c.Enabled = true;
                            return false;
                        }
                    }
                    if (CommandState.ContainsKey(username))
                    {
                        CommandState[username] = true;
                        return false;
                    }
                }
            }
            return false;
        }
        public bool disable(chat chat, List<string> msgs, bool ismod, bool pm, User CurUser)
        {
            if (CommandState["disable"] && msgs.Count > 3 && ismod)
            {
                if (CurUser.UserType == "op")
                {
                    string username = "";
                    GetName(msgs, 2, out username);


                    foreach (Command c in Commands)
                    {
                        if (username == c.sCommand)
                        {
                            c.Enabled = false;
                            return false;
                        }
                    }
                    if (CommandState.ContainsKey(username))
                    {
                        CommandState[username] = false;
                        return false;
                    }
                }
            }
            return false;
        }
        private string Convert(string from, string to, double iamount)
        {

            double From = getCurrencyValue(from);
            double To = getCurrencyValue(to);
            if (From == -1 || To == -1)
            {
                return "Invalid Currency";
            }
            else
            {
                return string.Format("{0:0.########} {1} = {2:0.########} {3}", iamount, from, (From / To) * iamount, to);
            }
        }
        /// <summary>
        /// Extracts the name from list of words. if a words[startpos] starts with ' or ", it adds the following words to the
        /// name untill it finds one ending with ' or ". 
        /// if no such word is found the word at index startPos is returned
        /// 
        /// Returns the index of the first string after the name
        /// </summary>
        /// <param name="Words"></param>
        /// <param name="startPos"></param>
        /// <returns></returns>
        int GetName(List<string> Words, int startPos, out string username)
        {
            username = "";
            int end = startPos + 1;
            if (Words[startPos][0] == '"' || Words[startPos][0] == '\'')
            {
                for (int i = startPos; i < Words.Count; i++)
                {
                    username += (Words[i]);
                    if ((Words[i][Words[i].Length - 1] == '\'' || Words[i][Words[i].Length - 1] == '"'))
                    {
                        end = i + 1;
                        break;
                    }
                    else
                    {
                        username += " ";
                    }
                }
            }
            else
            {
                username = Words[startPos];
            }
            username = username.Replace("\"", "").Replace("'", "");
            return end;
        }

        /// <summary>
        /// rebuilds a message from a string array. starting at the start index
        /// </summary>
        /// <param name="Words">list of works to build message with</param>
        /// <param name="start">inclusive starting index</param>
        /// <returns></returns>
        string BuildString(List<string> Words, int start)
        {
            string tmp = "";
            for (int i = start; i < Words.Count; i++)
            {
                tmp += Words[i] + (i < Words.Count - 1 ? " " : "");
            }
            return tmp;
        }


        #endregion


        #region Rainy Things
        int raincounter = 0;
        DateTime LastRain = DateTime.Now;
        int MinScoreRain = 5;
        int times = 0;
        decimal total = 0;

        /// <summary>
        /// Creates a list (as an array) of randomly selected, eligible users to be rained on.
        /// </summary>
        /// <param name="Amount">The number of users to select to rain on.</param>
        /// <returns>list of selected, elegibile users</returns>
        User[] getrandomids(int Amount)
        {
            List<User> ValidUsers = new List<User>();
            foreach (User U in activeusers)
            {
                if (U.getscore() >MinScoreRain)
                {
                    ValidUsers.Add(U);
                }
            }
            if (Amount >= ValidUsers.Count)
                return ValidUsers.ToArray();
            else
            {
                List<User> SelectedUsers = new List<User>();
                while (SelectedUsers.Count<Amount)
                {
                    int Add = r.Next(0, ValidUsers.Count - 1);
                    SelectedUsers.Add(ValidUsers[Add]);
                    ValidUsers.RemoveAt(Add);
                }
                return SelectedUsers.ToArray();
            }
        }

        /// <summary>
        /// Used to determine when to rain.
        /// </summary>
        /// <param name="State">Unused</param>
        void tmrRainTick(object State)
        {
            raincounter++;
            if ((DateTime.Now - LastRain).TotalSeconds > RainINterval.TotalSeconds)
            {
                LastRain = DateTime.Now;
                raincounter = 0;
                int count = 0;
                if (RainEnabled)
                {
                    foreach (User u in activeusers)
                    {
                        if (u.getscore() > 5)
                            count++;
                        if (count >= 3)
                            break;
                    }
                    if (count >= 3)
                        Rain(false);
                }
            }
            
        }

        /// <summary>
        /// Select usrs, determine amount and iniate the rain command for the site handler. Also logs rain in DB
        /// </summary>
        /// <param name="Forced">Whether the rain is forced by the operator/admin/mod using a command or from site handler. True=forced, false=natural rain</param>
        public void Rain(bool Forced)
        {
            if (GetBalance != null && SendRain!=null)
            {
                decimal balance = (decimal)GetBalance();
                User[] userstorain = getrandomids(1);
                if (Forced)
                {
                    userstorain = new User[1];
                    userstorain[0] = activeusers[r.Next(0, activeusers.Count)];
                }
                decimal rainAmount = SetRainAmount(balance);

                if (balance > rainAmount && userstorain.Length > 0)
                {
                    writelog("Rained " + rainAmount.ToString("0.0000") + " to " + userstorain[0], User.FindUser("seuntjie"));
                    if (Forced)
                    {
                        writelog("Rained Forced by click", User.FindUser("seuntjie"));
                    }
                    string tx = "";
                    if (SendRain(userstorain[0], (double)rainAmount))
                    {
                        MSSQL.Instance().RainAdd((double)rainAmount, (int)userstorain[0].Uid, DateTime.Now);
                        MessageQueue.Enqueue(new SendMessage { Message = string.Format("Congratulations {0}, {1:0.00000} rain coming your way", userstorain[0].Username, rainAmount), Pm = false, ToUser = userstorain[0] });
                        
                    }
                }
                gettotalRains();
            }
        }

        /// <summary>
        /// Determines the rain amount based on rainpercentage and minumum bet value
        /// </summary>
        /// <param name="bal">The current balance of the bot</param>
        /// <returns>Amount to rain</returns>
        decimal SetRainAmount(decimal bal)
        {
            decimal tmpAmount = bal * RainPercentage;
            if (tmpAmount >= MinRain)
                return tmpAmount;
            else
                return MinRain;
        }

        /// <summary>
        /// Gets the total amount rained
        /// </summary>
        void gettotalRains()
        {
            double[] rians = SQLBASE.Instance().GetTotalRained();
            total = (decimal)rians[0];
            times = (int)rians[1];
        }
        #endregion

        /// <summary>
        /// Works through the message queue and prevents sending messages faster than 1 message/second
        /// </summary>
        /// <param name="State">Unused</param>
        void tmrSendTick(object State)
        {
            if ((DateTime.Now - LastSent).TotalMilliseconds>=1000 && MessageQueue.Count>0)
            {
                if (SendMessage!=null)
                    SendMessage(MessageQueue.Dequeue());
            }
        }

        /// <summary>
        /// also checks user requests and removes them if the are older than the minimum spacing
        /// Check active users, remove from active list if messages are older than 20 minutes, updates score for each user as well
        /// </summary>
        /// <param name="State">Unused</param>
        void tmrUsersTick(object State)
        {
            
            for (int i = 0; i < activeusers.Count; i++)
            {
                if ((DateTime.Now - activeusers[i].LastActive).Minutes >= 20)
                {
                    activeusers.RemoveAt(i--);
                }
                else
                {
                    for (int j = 0; j < activeusers[i].Score.Count; j++)
                    {
                        if ((DateTime.Now - activeusers[i].Score[j].time).Minutes >= 20)
                        {
                            activeusers[i].Score.RemoveAt(j--);
                        }
                        else
                            break;
                    }
                    for (int j = 0; j < activeusers[i].CommandScore.Count; j++)
                    {
                        if ((DateTime.Now - activeusers[i].CommandScore[j].time).TotalSeconds >= 60)
                        {
                            activeusers[i].CommandScore.RemoveAt(j--);
                        }
                        else
                            break;
                    }
                }

            }

        }

        /// <summary>
        /// saves data to the log for security reasons
        /// </summary>
        /// <param name="msg">Message/action to log</param>
        /// <param name="cur">User initiating actions/message</param>
        public void writelog(string msg, User cur)
        {
            MSSQL.Instance().logCommand(msg, (int)cur.Uid);
        }

        #region users


        /// <summary>
        /// Checks whether a user has been issued a warning in the last ten minutes
        /// </summary>
        /// <param name="CurUser">The user to check</param>
        /// <returns></returns>
        bool checkwarning(User CurUser)
        {
            if (CurUser.Warning == null)
                return false;
            else
            {
                if ((DateTime.Now - CurUser.Warning).TotalSeconds > 600)
                {
                    return false;
                }
                else
                    return true;
            }
        }

               
        /// <summary>
        /// updates the last time a user was active in the chat.
        /// updates user score
        /// </summary>
        /// <param name="id"></param>
        /// <param name="score"></param>
        void updateactivetime(User curuser, int score)
        {
            bool found = false;
            foreach (User u in activeusers)
            {
                if (u == curuser)
                {
                    found = true;
                    u.Score.Add(new score(score, DateTime.Now));
                    u.LastActive = DateTime.Now;
                }
            }
            if (!found)
            {

                User tmp = curuser;
                tmp.Score = new List<score>();

                tmp.Score.Add(new score(score, DateTime.Now));
                tmp.LastActive = DateTime.Now;
                activeusers.Add(tmp);

            }
        }
        #endregion

        #region Currency Convertions
        void tmrCurrencyTick(object State)
        {
            Thread t = new Thread(new ThreadStart(getprices));
            t.Start();
        }

        double getCurrencyValue(string Currency)
        {
            try
            {
                PoloniexPair tmp = (PoloniexPair)Currencies.GetType().GetProperty("BTC_" + Currency.ToUpper()).GetValue(Currencies, null);
                if (tmp != null)
                {
                    return double.Parse(tmp.last);
                }
            }
            catch
            {
                if (Currency.ToLower() == "btc")
                    return 1.0;
                if (Currency.ToLower() == "usd")
                {
                    return 1.0 / (double)mbtc;
                }
                foreach (YahooPair yp in Fiat)
                {
                    if (yp.id.Substring(3).ToLower() == Currency.ToLower())
                    {
                        return (1.0 / yp.Rate) / (double)mbtc;
                    }
                }
            }
            return -1;
        }

        string GetCurrency(string Currency)
        {
            try
            {
                PoloniexPair tmp = (PoloniexPair)Currencies.GetType().GetProperty("BTC_" + Currency.ToUpper()).GetValue(Currencies, null);
                if (tmp != null)
                {
                    return string.Format("Current {0}/Btc at Poloniex: Highest Bid: {1} Btc, Lowest Ask: {2} Btc.", Currency, tmp.highestBid, tmp.lowestAsk);
                }
            }
            catch (Exception e)
            {
                dumperror(e.Message);

            }
            return "-1";
        }
        poloniexMarket Currencies;
        List<YahooPair> Fiat = new List<YahooPair>();
        private decimal mbtc;
        void getprices()
        {

            try
            {
                var tmprequest = (HttpWebRequest)HttpWebRequest.Create("https://poloniex.com/public?command=returnTicker");
                HttpWebResponse EmitResponse = (HttpWebResponse)tmprequest.GetResponse();
                string sEmitResponse = new StreamReader(EmitResponse.GetResponseStream()).ReadToEnd();
                Currencies = JsonDeserialize<poloniexMarket>(sEmitResponse.Replace("return", "returns"));


            }
            catch (Exception e)
            {
                dumperror(e.Message);
            }
            try
            {
                var tmprequest = (HttpWebRequest)HttpWebRequest.Create("https://api.bitcoinaverage.com/ticker/global/USD/");
                HttpWebResponse EmitResponse = (HttpWebResponse)tmprequest.GetResponse();
                string sEmitResponse = new StreamReader(EmitResponse.GetResponseStream()).ReadToEnd();
                btcPrice btc = JsonDeserialize<btcPrice>(sEmitResponse);
                this.mbtc = (btc.last);
            }
            catch (Exception e)
            {
                dumperror(e.Message);
            }
            try
            {
                string tmpURL = "https://query.yahooapis.com/v1/public/yql?q=select%20*%20from%20yahoo.finance.xchange%20where%20pair%20in%20(%22USDAED%22%2C%22USDAFN%22%2C%22USDALL%22%2C%22USDAMD%22%2C%22USDANG%22%2C%22USDAOA%22%2C%22USDARS%22%2C%22USDAUD%22%2C%22USDAWG%22%2C%22USDAZN%22%2C%22USDBAM%22%2C%22USDBBD%22%2C%22USDBDT%22%2C%22USDBGN%22%2C%22USDBHD%22%2C%22USDBIF%22%2C%22USDBMD%22%2C%22USDBND%22%2C%22USDBOB%22%2C%22USDBRL%22%2C%22USDBSD%22%2C%22USDBTN%22%2C%22USDBWP%22%2C%22USDBYR%22%2C%22USDBZD%22%2C%22USDCAD%22%2C%22USDCDF%22%2C%22USDCHF%22%2C%22USDCLF%22%2C%22USDCLP%22%2C%22USDCNY%22%2C%22USDCOP%22%2C%22USDCRC%22%2C%22USDCUP%22%2C%22USDCVE%22%2C%22USDCYP%22%2C%22USDCZK%22%2C%22USDDJF%22%2C%22USDDKK%22%2C%22USDDOP%22%2C%22USDDZD%22%2C%22USDEGP%22%2C%22USDERN%22%2C%22USDETB%22%2C%22USDEUR%22%2C%22USDFJD%22%2C%22USDFKP%22%2C%22USDGBP%22%2C%22USDGEL%22%2C%22USDGHS%22%2C%22USDGIP%22%2C%22USDGMD%22%2C%22USDGNF%22%2C%22USDGTQ%22%2C%22USDGYD%22%2C%22USDHKD%22%2C%22USDHNL%22%2C%22USDHRK%22%2C%22USDHTG%22%2C%22USDHUF%22%2C%22USDIDR%22%2C%22USDILS%22%2C%22USDINR%22%2C%22USDIQD%22%2C%22USDIRR%22%2C%22USDISK%22%2C%22USDJMD%22%2C%22USDJOD%22%2C%22USDJPY%22%2C%22USDKES%22%2C%22USDKGS%22%2C%22USDKHR%22%2C%22USDKMF%22%2C%22USDKPW%22%2C%22USDKRW%22%2C%22USDKWD%22%2C%22USDKYD%22%2C%22USDKZT%22%2C%22USDLAK%22%2C%22USDLBP%22%2C%22USDLKR%22%2C%22USDLRD%22%2C%22USDLSL%22%2C%22USDLTL%22%2C%22USDLVL%22%2C%22USDLYD%22%2C%22USDMAD%22%2C%22USDMDL%22%2C%22USDMGA%22%2C%22USDMKD%22%2C%22USDMMK%22%2C%22USDMNT%22%2C%22USDMOP%22%2C%22USDMRO%22%2C%22USDMUR%22%2C%22USDMVR%22%2C%22USDMWK%22%2C%22USDMXN%22%2C%22USDMXV%22%2C%22USDMYR%22%2C%22USDMZN%22%2C%22USDNAD%22%2C%22USDNGN%22%2C%22USDNIO%22%2C%22USDNOK%22%2C%22USDNPR%22%2C%22USDNZD%22%2C%22USDOMR%22%2C%22USDPAB%22%2C%22USDPEN%22%2C%22USDPGK%22%2C%22USDPHP%22%2C%22USDPKR%22%2C%22USDPLN%22%2C%22USDPYG%22%2C%22USDQAR%22%2C%22USDRON%22%2C%22USDRSD%22%2C%22USDRUB%22%2C%22USDRWF%22%2C%22USDSAR%22%2C%22USDSBD%22%2C%22USDSCR%22%2C%22USDSDG%22%2C%22USDSEK%22%2C%22USDSGD%22%2C%22USDSHP%22%2C%22USDSLL%22%2C%22USDSOS%22%2C%22USDSRD%22%2C%22USDSTD%22%2C%22USDSYP%22%2C%22USDSZL%22%2C%22USDTHB%22%2C%22USDTJS%22%2C%22USDTND%22%2C%22USDTOP%22%2C%22USDTRY%22%2C%22USDTTD%22%2C%22USDTWD%22%2C%22USDTZS%22%2C%22USDUAH%22%2C%22USDUGX%22%2C%22USDUSD%22%2C%22USDUYU%22%2C%22USDUZS%22%2C%22USDVND%22%2C%22USDVUV%22%2C%22USDWST%22%2C%22USDXAF%22%2C%22USDXAG%22%2C%22USDXAU%22%2C%22USDXCD%22%2C%22USDXDR%22%2C%22USDXOF%22%2C%22USDXPD%22%2C%22USDXPF%22%2C%22USDXPT%22%2C%22USDYER%22%2C%22USDZAR%22%2C%22USDZMK%22)&format=json&env=store%3A%2F%2Fdatatables.org%2Falltableswithkeys&callback=";
                var tmprequest = (HttpWebRequest)HttpWebRequest.Create(tmpURL);
                HttpWebResponse EmitResponse = (HttpWebResponse)tmprequest.GetResponse();
                string sEmitResponse = new StreamReader(EmitResponse.GetResponseStream()).ReadToEnd();
                Fiat = JsonDeserialize<YahooRequest>(sEmitResponse).query.results.rate;
            }
            catch (Exception e)
            {
                dumperror(e.Message);
            }

        }

        private void dumperror(string p)
        {
            //throw new NotImplementedException();
        }
        #endregion

    }
}
