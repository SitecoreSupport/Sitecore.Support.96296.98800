using Sitecore.Diagnostics;
using Sitecore.SessionProvider;
using System;
using System.Data;
using System.Data.SqlClient;
using System.Web.SessionState;

namespace Sitecore.Support.SessionProvider.Sql
{
    internal sealed class SqlSessionStateStore
    {
        private readonly bool m_Compress;

        private readonly string m_ConnectionString;

        internal SqlSessionStateStore(string connectionString, bool compress)
        {
            this.m_ConnectionString = connectionString;
            this.m_Compress = compress;
        }

        internal Guid GetApplicationIdentifier(string name)
        {
            Guid result;
            using (SqlCommand sqlCommand = new SqlCommand())
            {
                sqlCommand.CommandText = "[dbo].[GetApplicationId]";
                sqlCommand.CommandType = CommandType.StoredProcedure;
                SqlParameter value = new SqlParameter
                {
                    ParameterName = "@name",
                    SqlDbType = SqlDbType.NVarChar,
                    Size = 280,
                    Value = name
                };
                SqlParameter sqlParameter = new SqlParameter
                {
                    ParameterName = "@id",
                    SqlDbType = SqlDbType.UniqueIdentifier,
                    Direction = ParameterDirection.Output
                };
                sqlCommand.Parameters.Add(value);
                sqlCommand.Parameters.Add(sqlParameter);
                using (SqlConnection sqlConnection = new SqlConnection(this.m_ConnectionString))
                {
                    sqlConnection.Open();
                    sqlCommand.Connection = sqlConnection;
                    sqlCommand.ExecuteNonQuery();
                }
                result = (Guid)sqlParameter.Value;
            }
            return result;
        }

        internal SessionStateStoreData GetExpiredItemExclusive(Guid application, SessionStateLockCookie lockCookie, out string id)
        {
            id = null;
            SessionStateStoreData result = null;
            using (SqlCommand sqlCommand = new SqlCommand())
            {
                sqlCommand.CommandText = "[dbo].[GetExpiredItemExclusive]";
                sqlCommand.CommandType = CommandType.StoredProcedure;
                SqlParameter value = new SqlParameter
                {
                    ParameterName = "@application",
                    SqlDbType = SqlDbType.UniqueIdentifier,
                    Value = application
                };
                SqlParameter sqlParameter = new SqlParameter
                {
                    ParameterName = "@id",
                    SqlDbType = SqlDbType.NVarChar,
                    Size = 88,
                    Direction = ParameterDirection.Output
                };
                SqlParameter value2 = new SqlParameter
                {
                    ParameterName = "@lockTimestamp",
                    SqlDbType = SqlDbType.DateTime,
                    Value = lockCookie.Timestamp
                };
                SqlParameter value3 = new SqlParameter
                {
                    ParameterName = "@lockCookie",
                    SqlDbType = SqlDbType.VarChar,
                    Size = 32,
                    Value = lockCookie.Id
                };
                SqlParameter sqlParameter2 = new SqlParameter
                {
                    ParameterName = "@result",
                    SqlDbType = SqlDbType.Int,
                    Direction = ParameterDirection.ReturnValue
                };
                sqlCommand.Parameters.Add(value);
                sqlCommand.Parameters.Add(sqlParameter);
                sqlCommand.Parameters.Add(value2);
                sqlCommand.Parameters.Add(value3);
                sqlCommand.Parameters.Add(sqlParameter2);
                int num = 0;
                byte[] data = null;
                using (SqlConnection sqlConnection = new SqlConnection(this.m_ConnectionString))
                {
                    sqlConnection.Open();
                    sqlCommand.Connection = sqlConnection;
                    using (SqlDataReader sqlDataReader = sqlCommand.ExecuteReader(CommandBehavior.SingleRow))
                    {
                        if (sqlDataReader.Read() && !sqlDataReader.IsDBNull(0))
                        {
                            data = (byte[])sqlDataReader[0];
                        }
                    }
                    num = (int)sqlParameter2.Value;
                }
                if (num == 1)
                {
                    id = (string)sqlParameter.Value;
                    result = SessionStateSerializer.Deserialize(data);
                }
            }
            return result;
        }

        private bool IsItemExist(string id)
        {
            using (SqlCommand sqlCommand = new SqlCommand())
            {
                sqlCommand.CommandText = "SELECT [id] FROM [dbo].[SessionState] WHERE [id]=@id";
                sqlCommand.CommandType = CommandType.Text;
                SqlParameter value = new SqlParameter
                {
                    ParameterName = "@id",
                    SqlDbType = SqlDbType.NVarChar,
                    Size = 88,
                    Value = id
                };
                sqlCommand.Parameters.Add(value);
                using (SqlConnection sqlConnection = new SqlConnection(this.m_ConnectionString))
                {
                    sqlConnection.Open();
                    sqlCommand.Connection = sqlConnection;
                    using (SqlDataReader sqlDataReader = sqlCommand.ExecuteReader(CommandBehavior.SingleRow))
                    {
                        if (sqlDataReader.Read() && !sqlDataReader.IsDBNull(0))
                        {
                            return true;
                        }
                    }
                }
            }
            return false;
        }

        internal SessionStateStoreData GetItem(Guid application, string id, out SessionStateLockCookie lockCookie, out int flags)
        {
            lockCookie = null;
            flags = 0;
            SessionStateStoreData sessionStateStoreData = null;
            SessionStateStoreData result;
            using (SqlCommand sqlCommand = new SqlCommand())
            {
                sqlCommand.CommandText = "[dbo].[GetItem]";
                sqlCommand.CommandType = CommandType.StoredProcedure;
                SqlParameter value = new SqlParameter
                {
                    ParameterName = "@application",
                    SqlDbType = SqlDbType.UniqueIdentifier,
                    Value = application
                };
                SqlParameter value2 = new SqlParameter
                {
                    ParameterName = "@id",
                    SqlDbType = SqlDbType.NVarChar,
                    Size = 88,
                    Value = id
                };
                SqlParameter sqlParameter = new SqlParameter
                {
                    ParameterName = "@locked",
                    SqlDbType = SqlDbType.Bit,
                    Direction = ParameterDirection.Output
                };
                SqlParameter sqlParameter2 = new SqlParameter
                {
                    ParameterName = "@lockTimestamp",
                    SqlDbType = SqlDbType.DateTime,
                    Direction = ParameterDirection.Output
                };
                SqlParameter sqlParameter3 = new SqlParameter
                {
                    ParameterName = "@lockCookie",
                    SqlDbType = SqlDbType.VarChar,
                    Size = 32,
                    Direction = ParameterDirection.Output
                };
                SqlParameter sqlParameter4 = new SqlParameter
                {
                    ParameterName = "@flags",
                    SqlDbType = SqlDbType.Int,
                    Direction = ParameterDirection.Output
                };
                SqlParameter sqlParameter5 = new SqlParameter
                {
                    ParameterName = "@result",
                    SqlDbType = SqlDbType.Int,
                    Direction = ParameterDirection.ReturnValue
                };
                sqlCommand.Parameters.Add(value);
                sqlCommand.Parameters.Add(value2);
                sqlCommand.Parameters.Add(sqlParameter);
                sqlCommand.Parameters.Add(sqlParameter2);
                sqlCommand.Parameters.Add(sqlParameter3);
                sqlCommand.Parameters.Add(sqlParameter4);
                sqlCommand.Parameters.Add(sqlParameter5);
                int num = 0;
                byte[] array = null;
                using (SqlConnection sqlConnection = new SqlConnection(this.m_ConnectionString))
                {
                    sqlConnection.Open();
                    sqlCommand.Connection = sqlConnection;
                    using (SqlDataReader sqlDataReader = sqlCommand.ExecuteReader(CommandBehavior.SingleRow))
                    {
                        if (sqlDataReader.Read() && !sqlDataReader.IsDBNull(0))
                        {
                            array = (byte[])sqlDataReader[0];
                        }
                    }
                    num = (int)sqlParameter5.Value;
                }
                if (num != 1)
                {
                    result = sessionStateStoreData;
                }
                else
                {
                    flags = (int)sqlParameter4.Value;
                    bool flag3 = (bool)sqlParameter.Value;
                    string id2 = sqlParameter3.Value.ToString();
                    DateTime dateTime = (DateTime)sqlParameter2.Value;
                    dateTime = DateTime.SpecifyKind(dateTime, DateTimeKind.Utc);
                    if (flag3)
                    {
                        lockCookie = new SessionStateLockCookie(id2, dateTime);
                        result = sessionStateStoreData;
                    }
                    else
                    {
                        Assert.IsNotNull(array, "The session item was not returned from the database.");
                        result = SessionStateSerializer.Deserialize(array);
                    }
                }
            }
            return result;
        }

        internal SessionStateStoreData GetItemExclusive(Guid application, string id, SessionStateLockCookie acquiredLockCookie, out SessionStateLockCookie existingLockCookie, out int flags)
        {
            flags = 0;
            existingLockCookie = null;
            SessionStateStoreData result = null;
            using (SqlCommand sqlCommand = new SqlCommand())
            {
                sqlCommand.CommandText = "[dbo].[GetItemExclusive]";
                sqlCommand.CommandType = CommandType.StoredProcedure;
                SqlParameter value = new SqlParameter
                {
                    ParameterName = "@application",
                    SqlDbType = SqlDbType.UniqueIdentifier,
                    Value = application
                };
                SqlParameter value2 = new SqlParameter
                {
                    ParameterName = "@id",
                    SqlDbType = SqlDbType.NVarChar,
                    Size = 88,
                    Value = id
                };
                SqlParameter sqlParameter = new SqlParameter
                {
                    ParameterName = "@locked",
                    SqlDbType = SqlDbType.Bit,
                    Direction = ParameterDirection.Output
                };
                SqlParameter sqlParameter2 = new SqlParameter
                {
                    ParameterName = "@lockTimestamp",
                    SqlDbType = SqlDbType.DateTime,
                    Direction = ParameterDirection.Output
                };
                SqlParameter sqlParameter3 = new SqlParameter
                {
                    ParameterName = "@lockCookie",
                    SqlDbType = SqlDbType.VarChar,
                    Size = 32,
                    Direction = ParameterDirection.InputOutput,
                    Value = acquiredLockCookie.Id
                };
                SqlParameter value3 = new SqlParameter
                {
                    ParameterName = "@flags",
                    SqlDbType = SqlDbType.Int,
                    Direction = ParameterDirection.Output
                };
                SqlParameter sqlParameter4 = new SqlParameter
                {
                    ParameterName = "@result",
                    SqlDbType = SqlDbType.Int,
                    Direction = ParameterDirection.ReturnValue
                };
                sqlCommand.Parameters.Add(value);
                sqlCommand.Parameters.Add(value2);
                sqlCommand.Parameters.Add(sqlParameter);
                sqlCommand.Parameters.Add(sqlParameter2);
                sqlCommand.Parameters.Add(sqlParameter3);
                sqlCommand.Parameters.Add(value3);
                sqlCommand.Parameters.Add(sqlParameter4);
                int num = 0;
                byte[] array = null;
                using (SqlConnection sqlConnection = new SqlConnection(this.m_ConnectionString))
                {
                    sqlConnection.Open();
                    sqlCommand.Connection = sqlConnection;
                    using (SqlDataReader sqlDataReader = sqlCommand.ExecuteReader(CommandBehavior.SingleRow))
                    {
                        if (sqlDataReader.Read() && !sqlDataReader.IsDBNull(0))
                        {
                            array = (byte[])sqlDataReader[0];
                        }
                    }
                    num = (int)sqlParameter4.Value;
                }
                if (num != 1)
                {
                    return result;
                }
                if ((bool)sqlParameter.Value)
                {
                    string id2 = (string)sqlParameter3.Value;
                    DateTime dateTime = (DateTime)sqlParameter2.Value;
                    dateTime = DateTime.SpecifyKind(dateTime, DateTimeKind.Utc);
                    existingLockCookie = new SessionStateLockCookie(id2, dateTime);
                }
                if (array != null)
                {
                    result = SessionStateSerializer.Deserialize(array);
                }
            }
            return result;
        }

        internal void InsertItem(Guid application, string id, int flags, SessionStateStoreData sessionState)
        {
            try
            {
                if (this.IsItemExist(id))
                {
                    Log.Debug("Attempting to insert a duplicate key into the SQL Session Store. Entry skipped.");
                    return;
                }
                byte[] value = SessionStateSerializer.Serialize(sessionState, this.m_Compress);
                using (SqlCommand sqlCommand = new SqlCommand())
                {
                    sqlCommand.CommandText = "[dbo].[InsertItem]";
                    sqlCommand.CommandType = CommandType.StoredProcedure;
                    SqlParameter value2 = new SqlParameter
                    {
                        ParameterName = "@application",
                        SqlDbType = SqlDbType.UniqueIdentifier,
                        Value = application
                    };
                    SqlParameter value3 = new SqlParameter
                    {
                        ParameterName = "@id",
                        SqlDbType = SqlDbType.NVarChar,
                        Size = 88,
                        Value = id
                    };
                    SqlParameter value4 = new SqlParameter
                    {
                        ParameterName = "@item",
                        SqlDbType = SqlDbType.Image,
                        Value = value
                    };
                    SqlParameter value5 = new SqlParameter
                    {
                        ParameterName = "@timeout",
                        SqlDbType = SqlDbType.Int,
                        Value = sessionState.Timeout
                    };
                    SqlParameter value6 = new SqlParameter
                    {
                        ParameterName = "@flags",
                        SqlDbType = SqlDbType.Int,
                        Value = flags
                    };
                    sqlCommand.Parameters.Add(value2);
                    sqlCommand.Parameters.Add(value3);
                    sqlCommand.Parameters.Add(value4);
                    sqlCommand.Parameters.Add(value5);
                    sqlCommand.Parameters.Add(value6);
                    using (SqlConnection sqlConnection = new SqlConnection(this.m_ConnectionString))
                    {
                        sqlConnection.Open();
                        sqlCommand.Connection = sqlConnection;
                        sqlCommand.ExecuteNonQuery();
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error("Sitecore.Support.96296.98800: " + ex, this);
            }
        }

        internal void ReleaseItem(Guid application, string id, string lockCookie)
        {
            using (SqlCommand sqlCommand = new SqlCommand())
            {
                sqlCommand.CommandText = "[dbo].[ReleaseItem]";
                sqlCommand.CommandType = CommandType.StoredProcedure;
                SqlParameter value = new SqlParameter
                {
                    ParameterName = "@application",
                    SqlDbType = SqlDbType.UniqueIdentifier,
                    Value = application
                };
                SqlParameter value2 = new SqlParameter
                {
                    ParameterName = "@id",
                    SqlDbType = SqlDbType.NVarChar,
                    Size = 88,
                    Value = id
                };
                SqlParameter value3 = new SqlParameter
                {
                    ParameterName = "@lockCookie",
                    SqlDbType = SqlDbType.VarChar,
                    Size = 32,
                    Value = lockCookie
                };
                SqlParameter sqlParameter = new SqlParameter
                {
                    ParameterName = "@result",
                    SqlDbType = SqlDbType.Int,
                    Direction = ParameterDirection.ReturnValue
                };
                sqlCommand.Parameters.Add(value);
                sqlCommand.Parameters.Add(value2);
                sqlCommand.Parameters.Add(value3);
                sqlCommand.Parameters.Add(sqlParameter);
                using (SqlConnection sqlConnection = new SqlConnection(this.m_ConnectionString))
                {
                    sqlConnection.Open();
                    sqlCommand.Connection = sqlConnection;
                    sqlCommand.ExecuteNonQuery();
                    int arg_111_0 = (int)sqlParameter.Value;
                }
            }
        }

        internal void RemoveItem(Guid application, string id, string lockCookie)
        {
            using (SqlCommand sqlCommand = new SqlCommand())
            {
                sqlCommand.CommandText = "[dbo].[RemoveItem]";
                sqlCommand.CommandType = CommandType.StoredProcedure;
                SqlParameter value = new SqlParameter
                {
                    ParameterName = "@application",
                    SqlDbType = SqlDbType.UniqueIdentifier,
                    Value = application
                };
                SqlParameter value2 = new SqlParameter
                {
                    ParameterName = "@id",
                    SqlDbType = SqlDbType.NVarChar,
                    Size = 88,
                    Value = id
                };
                SqlParameter value3 = new SqlParameter
                {
                    ParameterName = "@lockCookie",
                    SqlDbType = SqlDbType.VarChar,
                    Size = 32,
                    Value = lockCookie
                };
                SqlParameter sqlParameter = new SqlParameter
                {
                    ParameterName = "@result",
                    SqlDbType = SqlDbType.Int,
                    Direction = ParameterDirection.ReturnValue
                };
                sqlCommand.Parameters.Add(value);
                sqlCommand.Parameters.Add(value2);
                sqlCommand.Parameters.Add(value3);
                sqlCommand.Parameters.Add(sqlParameter);
                using (SqlConnection sqlConnection = new SqlConnection(this.m_ConnectionString))
                {
                    sqlConnection.Open();
                    sqlCommand.Connection = sqlConnection;
                    sqlCommand.ExecuteNonQuery();
                    int arg_111_0 = (int)sqlParameter.Value;
                }
            }
        }

        internal void UpdateAndReleaseItem(Guid application, string id, string lockCookie, SessionStateActions action, SessionStateStoreData sessionState)
        {
            using (SqlCommand sqlCommand = new SqlCommand())
            {
                sqlCommand.CommandText = "[dbo].[SetAndReleaseItem]";
                sqlCommand.CommandType = CommandType.StoredProcedure;
                SqlParameter value = new SqlParameter
                {
                    ParameterName = "@application",
                    SqlDbType = SqlDbType.UniqueIdentifier,
                    Value = application
                };
                SqlParameter value2 = new SqlParameter
                {
                    ParameterName = "@id",
                    SqlDbType = SqlDbType.NVarChar,
                    Size = 88,
                    Value = id
                };
                SqlParameter value3 = new SqlParameter
                {
                    ParameterName = "@lockCookie",
                    SqlDbType = SqlDbType.VarChar,
                    Size = 32,
                    Value = lockCookie
                };
                SqlParameter value4 = new SqlParameter
                {
                    ParameterName = "@flags",
                    SqlDbType = SqlDbType.Int,
                    Value = action
                };
                SqlParameter value5 = new SqlParameter
                {
                    ParameterName = "@timeout",
                    SqlDbType = SqlDbType.Int,
                    Value = sessionState.Timeout
                };
                SqlParameter sqlParameter = new SqlParameter
                {
                    ParameterName = "@item",
                    SqlDbType = SqlDbType.Image
                };
                SqlParameter sqlParameter2 = new SqlParameter
                {
                    ParameterName = "@result",
                    SqlDbType = SqlDbType.Int,
                    Direction = ParameterDirection.ReturnValue
                };
                sqlParameter.Value = SessionStateSerializer.Serialize(sessionState, this.m_Compress);
                sqlCommand.Parameters.Add(value);
                sqlCommand.Parameters.Add(value2);
                sqlCommand.Parameters.Add(value3);
                sqlCommand.Parameters.Add(value4);
                sqlCommand.Parameters.Add(value5);
                sqlCommand.Parameters.Add(sqlParameter);
                sqlCommand.Parameters.Add(sqlParameter2);
                using (SqlConnection sqlConnection = new SqlConnection(this.m_ConnectionString))
                {
                    sqlConnection.Open();
                    sqlCommand.Connection = sqlConnection;
                    sqlCommand.ExecuteNonQuery();
                    int arg_1B9_0 = (int)sqlParameter2.Value;
                }
            }
        }

        internal void UpdateItemExpiration(Guid application, string id)
        {
            using (SqlCommand sqlCommand = new SqlCommand())
            {
                sqlCommand.CommandText = "[dbo].[ResetItemTimeout]";
                sqlCommand.CommandType = CommandType.StoredProcedure;
                SqlParameter value = new SqlParameter
                {
                    ParameterName = "@application",
                    SqlDbType = SqlDbType.UniqueIdentifier,
                    Value = application
                };
                SqlParameter value2 = new SqlParameter
                {
                    ParameterName = "@id",
                    SqlDbType = SqlDbType.NVarChar,
                    Size = 88,
                    Value = id
                };
                sqlCommand.Parameters.Add(value);
                sqlCommand.Parameters.Add(value2);
                using (SqlConnection sqlConnection = new SqlConnection(this.m_ConnectionString))
                {
                    sqlConnection.Open();
                    sqlCommand.Connection = sqlConnection;
                    sqlCommand.ExecuteNonQuery();
                }
            }
        }
    }
}