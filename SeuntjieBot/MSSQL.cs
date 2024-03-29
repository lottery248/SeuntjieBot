﻿using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SeuntjieBot
{
    class MSSQL:SQLBASE
    {
        SqlConnection GetCon()
        {
            return new SqlConnection(SeuntjieBot.sqlConnectionString); //"SERVER=localhost;DATABASE=md;integrated security=true");
        }
        internal  override User updateUser(User ToUpdate)
        {
            User tmp = null;
            SqlConnection sqcon = GetCon();
                
            try
            {
                
                SqlCommand com = new SqlCommand("USER_EDIT", sqcon);
                sqcon.Open();
                com.CommandType = System.Data.CommandType.StoredProcedure;
                com.Parameters.AddWithValue("USERNAME", ToUpdate.Username);
                com.Parameters.AddWithValue("ADDRESS", ToUpdate.Address);
                com.Parameters.AddWithValue("TITLE", ToUpdate.Title);
                com.Parameters.AddWithValue("NOTE", ToUpdate.Note);
                com.Parameters.AddWithValue("USERTYPE", ToUpdate.UserType);
                if (ToUpdate.Uid != -1)
                {
                    com.Parameters.AddWithValue("UID", ToUpdate.Uid);
                }
                SqlDataReader Reader = com.ExecuteReader();
                if (Reader.Read())
                tmp = UserParser(Reader);
            }
            catch
            {
                
            }
            sqcon.Close();
            return tmp;
        }

        internal  override User Usergetbyname(string Username)
        {
            User tmp = null;
            SqlConnection sqcon = GetCon();
            try
            {
                sqcon.Open();
                SqlCommand Command = new SqlCommand("get_user_name", sqcon);
                Command.CommandType = System.Data.CommandType.StoredProcedure;
                Command.Parameters.AddWithValue("username", Username);
                SqlDataReader Reader = Command.ExecuteReader();
                if (Reader.Read())
                {
                    tmp = UserParser(Reader);
                }
            }
            catch
            {

            }
            sqcon.Close();
            return tmp;
        }

        internal  override User UsergetbyID(int ID)
        {
            User tmp = null;
            SqlConnection sqcon = GetCon();
            try
            {
                sqcon.Open();
                SqlCommand Command = new SqlCommand("get_user_id", sqcon);
                Command.CommandType = System.Data.CommandType.StoredProcedure;
                Command.Parameters.AddWithValue("uid", ID);
                
                SqlDataReader Reader = Command.ExecuteReader();
                if (Reader.Read())
                {
                    tmp = UserParser(Reader);
                }
            }
            catch
            {

            }
            sqcon.Close();
            return tmp;
        }

        internal  override bool Redlist(User ToRedlist, string Reason)
        {
            SqlConnection sqcon = GetCon();
            bool success = false;
            SqlCommand com = new SqlCommand("redlist_add", sqcon);
            com.CommandType = System.Data.CommandType.StoredProcedure;
            com.Parameters.AddWithValue("username", ToRedlist.Uid);
            com.Parameters.AddWithValue("reason", Reason);
            try
            {
                sqcon.Open();
                com.ExecuteNonQuery();
                success = true;
            }
            catch (Exception e)
            {
                success = false;
            }
            sqcon.Close();
            return success;
        }

        internal  override bool DeRedlist(User ToRedlist)
        {
            SqlConnection sqcon = GetCon();
            bool success = false;
            SqlCommand com = new SqlCommand("redlist_remove", sqcon);
            com.CommandType = System.Data.CommandType.StoredProcedure;
            com.Parameters.AddWithValue("username", ToRedlist.Uid);
            
            try
            {
                sqcon.Open();
                com.ExecuteNonQuery();
                success = true;
            }
            catch (Exception e)
            {
                success = false;
            }
            sqcon.Close();
            return success;
        }

        internal  override bool BlackList(User ToRedlist, string Reason)
        {
            SqlConnection sqcon = GetCon();
            bool success = false;
            SqlCommand com = new SqlCommand("blacklist_add", sqcon);
            com.CommandType = System.Data.CommandType.StoredProcedure;
            com.Parameters.AddWithValue("username", ToRedlist.Uid);
            com.Parameters.AddWithValue("reason", Reason);
            try
            {
                sqcon.Open();
                com.ExecuteNonQuery();
                success = true;
            }
            catch (Exception e)
            {
                success = false;
            }
            sqcon.Close();
            return success;
        }

        internal  override bool DeBlacklist(User ToRedlist)
        {
            SqlConnection sqcon = GetCon();
            bool success = false;
            SqlCommand com = new SqlCommand("blacklist_remove", sqcon);
            com.CommandType = System.Data.CommandType.StoredProcedure;
            com.Parameters.AddWithValue("username", ToRedlist.Uid);
            
            try
            {
                sqcon.Open();
                com.ExecuteNonQuery();
                success = true;
            }
            catch (Exception e)
            {
                success = false;
            }
            sqcon.Close();
            return success;
        }

        internal  override bool LogMessage(chat Message)
        {
            bool ret = false;
            SqlConnection sqcon = GetCon();
            try
            {
                
                SqlCommand com = new SqlCommand("CHAT_ADD", sqcon);
                com.Parameters.AddWithValue("message", Message.Message);
                com.Parameters.AddWithValue("username", Message.User);
                com.Parameters.AddWithValue("time", Message.Time);
                if (Message.UID != -1)
                    com.Parameters.AddWithValue("uid", Message.UID);
                com.CommandType = CommandType.StoredProcedure;
                sqcon.Open();
                com.ExecuteNonQuery();
                ret = true;
                
            }
            catch
            {
                ret = false;
            }
            sqcon.Close();
            return ret;
        }

        internal  override double[] GetTotalRained()
        {
            SqlConnection sqcon = GetCon();
            double[] tmp = new double[2];
            SqlCommand com = new SqlCommand("rains_getTotal", sqcon);
            com.CommandType = System.Data.CommandType.StoredProcedure;
            
            try
            {
                sqcon.Open();
                SqlDataReader Reader = com.ExecuteReader();
                if (Reader.Read())
                {
                    tmp[0] = (double)(Reader["RAINED"]);
                    tmp[1] = (double)((int)Reader["TIMES"]);
                }

            }
            catch (Exception e)
            {

            }
            sqcon.Close();
            return tmp;
        }

        internal  override double[] GetUserRained(int uid)
        {
            SqlConnection sqcon = GetCon();
            double[] tmp = new double[2];
            SqlCommand com = new SqlCommand("user_getRAINS", sqcon);
            com.CommandType = System.Data.CommandType.StoredProcedure;
            com.Parameters.AddWithValue("username", uid);
                    
            try
            {
                sqcon.Open();
                SqlDataReader Reader = com.ExecuteReader();
                if (Reader.Read())
                {
                    tmp[0] = (double)(Reader["RAINED"]);
                    tmp[1] = (double)((int)Reader["TIMES"]);
                }
                
            }
            catch (Exception e)
            {
                
            }
            sqcon.Close();
            return tmp;
        }


        internal  override bool RainAdd(double amount, int uid, DateTime Time, int Instigator, bool forced)
        {
            string txid = "";
            bool valid = false;
            SqlConnection sqcon = GetCon();
            try
            {
                sqcon.Open();
                SqlCommand Command = new SqlCommand("Rain_ADD", sqcon);
                Command.Parameters.AddWithValue("amount", amount);
                Command.Parameters.AddWithValue("time", DateTime.UtcNow);
                Command.Parameters.AddWithValue("uid", Instigator);
                Command.Parameters.AddWithValue("force", forced);
                Command.CommandType = CommandType.StoredProcedure;
                SqlDataReader Reader = Command.ExecuteReader();
                if (Reader.Read())
                {
                    txid = Reader["txID"].ToString();
                }
                Reader.Close();
                if (txid != "")
                {
                    Command = new SqlCommand("rain_user_add", sqcon);

                    Command.Parameters.AddWithValue("txid", txid);
                    Command.Parameters.AddWithValue("username", uid);
                    Command.CommandType = CommandType.StoredProcedure;
                    Command.ExecuteNonQuery();
                    valid = true;
                }
            }
            catch (Exception e)
            {
                
                
            }
            sqcon.Close();
            return valid;
        }

        internal  override bool logCommand(string Message, int Uid)
        {
            //parent.AddMessage(cur.id + ": " + msg);
            SqlConnection Con = GetCon();
            bool valid = false;
            SqlCommand com = new SqlCommand("LOG_add", Con);
            com.CommandType = System.Data.CommandType.StoredProcedure;
            com.Parameters.AddWithValue("TIME", DateTime.Now);

            com.Parameters.AddWithValue("username", Uid);
            com.Parameters.AddWithValue("ENTRY", Message);

            try
            {
                Con.Open();
                com.ExecuteNonQuery();
                valid = true;
            }
            catch (Exception e)
            {
                //parent.dumperror(e.Message);
            }
            Con.Close();
            return valid;
        }

        internal  override int GetUserStatus(User GetFor)
        {
            SqlConnection sqcon = GetCon();
            int status = 0;
            SqlCommand com = new SqlCommand("getstatus", sqcon);
            com.CommandType = System.Data.CommandType.StoredProcedure;
            com.Parameters.AddWithValue("uid", GetFor.Uid);
            
            try
            {
                sqcon.Open();
                SqlDataReader Reader = com.ExecuteReader();
                if (Reader.Read())
                {
                    status = (int)Reader["status"];
                }
                
            }
            catch (Exception e)
            {
                
            }
            sqcon.Close();
            return status;
        }

        internal  override LateMessage[] GetMessagesForUser(User ToGet)
        {
            List<LateMessage> tmp = new List<LateMessage>();
            SqlConnection sqcon = GetCon();
            try
            {
                sqcon.Open();
                SqlCommand Command = new SqlCommand("GetLateMessage", sqcon);
                Command.CommandType = System.Data.CommandType.StoredProcedure;
                Command.Parameters.AddWithValue("uid", ToGet.Uid);

                SqlDataReader Reader = Command.ExecuteReader();
                while (Reader.Read())
                {
                    tmp.Add(MsgParser(Reader));
                }
            }
            catch
            {

            }
            sqcon.Close();
            return tmp.ToArray();
        }

        internal  override string GetBlackReasonForUser(User GetFor)
        {
            string tmp = null;
            SqlConnection sqcon = GetCon();
            try
            {
                sqcon.Open();
                SqlCommand Command = new SqlCommand("blacklist_get_reason", sqcon);
                Command.CommandType = System.Data.CommandType.StoredProcedure;
                Command.Parameters.AddWithValue("uid", GetFor.Uid);

                SqlDataReader Reader = Command.ExecuteReader();
                if (Reader.Read())
                {
                    tmp = (string)Reader["reason"];
                }
            }
            catch
            {

            }
            sqcon.Close();
            return tmp;
        }

        internal  override string GetRedReasonForUser(User GetFor)
        {
            string tmp = null;
            SqlConnection sqcon = GetCon();
            try
            {
                sqcon.Open();
                SqlCommand Command = new SqlCommand("redlist_get_reason", sqcon);
                Command.CommandType = System.Data.CommandType.StoredProcedure;
                Command.Parameters.AddWithValue("uid", GetFor.Uid);

                SqlDataReader Reader = Command.ExecuteReader();
                if (Reader.Read())
                {
                    tmp = (string)Reader["reason"];
                }
            }
            catch
            {

            }
            sqcon.Close();
            return tmp;
        }

        internal  override bool AddMessageForUser(LateMessage msg)
        {
            int tmp = 0;
            SqlConnection sqcon = GetCon();
            try
            {
                sqcon.Open();
                SqlCommand Command = new SqlCommand("addLateMessage", sqcon);
                Command.CommandType = System.Data.CommandType.StoredProcedure;
                Command.Parameters.AddWithValue("uid", msg.FromUid);
                Command.Parameters.AddWithValue("toID", msg.ToUid);
                Command.Parameters.AddWithValue("message", msg.Message);
                Command.Parameters.AddWithValue("pm", msg.pm?1:0);
                Command.Parameters.AddWithValue("time", msg.MessageTime);

                tmp = Command.ExecuteNonQuery();
            }
            catch
            {

            }
            sqcon.Close();
            return tmp!=0;
        }

        internal  override bool SentMessageForUser(LateMessage msg)
        {
            int tmp = 0;
            SqlConnection sqcon = GetCon();
            try
            {
                sqcon.Open();
                SqlCommand Command = new SqlCommand("MessageSent", sqcon);
                Command.CommandType = System.Data.CommandType.StoredProcedure;
                Command.Parameters.AddWithValue("id", msg.id);
                

                tmp = Command.ExecuteNonQuery();
            }
            catch
            {

            }
            sqcon.Close();
            return tmp != 0;
        }

        internal override decimal getTotalUsersBalance()
        {
            decimal tmp = 0;
            SqlConnection sqcon = GetCon();
            try
            {
                sqcon.Open();
                SqlCommand Command = new SqlCommand("getTotalUsersBalance", sqcon);
                Command.CommandType = System.Data.CommandType.StoredProcedure;

                SqlDataReader Reader = Command.ExecuteReader();
                if (Reader.Read())
                {
                    tmp = (decimal)Reader["balance"];
                }
            }
            catch
            {

            }
            sqcon.Close();
            return tmp;
        }

        internal override void ReduceUserBalance(long p, decimal Amount)
        {
            decimal tmp = 0;
            SqlConnection sqcon = GetCon();
            try
            {
                sqcon.Open();
                SqlCommand Command = new SqlCommand("ReduceUserBalance", sqcon);
                Command.CommandType = System.Data.CommandType.StoredProcedure;
                Command.Parameters.AddWithValue("uid", p);
                Command.Parameters.AddWithValue("amount", Amount);
                tmp = Command.ExecuteNonQuery();

                
            }
            catch
            {

            }
            sqcon.Close();
            
        }

        internal override void ReceivedTip(long p, double Amount, DateTime Time)
        {
            decimal tmp = 0;
            SqlConnection sqcon = GetCon();
            try
            {
                sqcon.Open();
                SqlCommand Command = new SqlCommand("TipReceived", sqcon);
                Command.CommandType = System.Data.CommandType.StoredProcedure;
                Command.Parameters.AddWithValue("uid", p);
                Command.Parameters.AddWithValue("amount", Amount);
                Command.Parameters.AddWithValue("time", Time);
                tmp = Command.ExecuteNonQuery();


            }
            catch
            {

            }
            sqcon.Close();
        }
    }
}
