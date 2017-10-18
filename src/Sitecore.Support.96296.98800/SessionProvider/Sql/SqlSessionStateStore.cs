using Sitecore.Diagnostics;
using System;
using System.Data;
using System.Data.SqlClient;
using System.Web.SessionState;

namespace Sitecore.Support.SessionProvider.Sql
{
  internal sealed class SqlSessionStateStore
  {
    private readonly string m_ConnectionString;
    private readonly bool m_Compress;


    /// <summary>
    ///   Initializes a new instance of the <see cref="SqlSessionStateStore"/> class.
    /// </summary>
    /// <param name="connectionString">
    ///   The connection string to use to connect to the Session State database.
    /// </param>
    /// <param name="compress">
    ///   Indicates whether to compress session state items.
    /// </param>
    internal SqlSessionStateStore([NotNull] string connectionString, bool compress)
    {
      Debug.ArgumentNotNull(connectionString, "connectionString");

      this.m_ConnectionString = connectionString;
      this.m_Compress = compress;
    }



    /// <summary>
    ///   Inserts a new session state store item into the sessions table.
    /// </summary>
    /// <param name="application">
    ///   The unique identifier of the application the session state item belongs to.
    /// </param>
    /// <param name="id">
    ///   The unique identifier of the session state store item.
    /// </param>
    /// <param name="flags">
    ///   An <see cref="int"/> value that is a set of flags stored together with the session state store item.
    /// </param>
    /// <param name="sessionState">
    ///   A <see cref="SessionStateStoreData"/> object that represents the session state.
    /// </param>
    /// <remarks>
    ///   This method insert a new row into the sessions table. The expiration field is set to the current
    ///   timestamp incremented by the timeout value specified in the session state. The new session state store 
    ///   item is not locked.
    /// </remarks>
    /// <exception cref="SqlException">
    ///   A session state store item with the specified identifier already exists.
    /// </exception>
    internal void InsertItem(Guid application, [NotNull] string id, int flags, [NotNull] SessionStateStoreData sessionState)
    {
      Debug.ArgumentNotNull(id, "id");
      Debug.ArgumentNotNull(sessionState, "sessionState");

      try
      {
        byte[] data = SessionStateSerializer.Serialize(sessionState, this.m_Compress);

        using (SqlCommand command = new SqlCommand())
        {
          command.CommandText = "[dbo].[InsertItem]";
          command.CommandType = CommandType.StoredProcedure;

          SqlParameter paramApplication = new SqlParameter
          {
            ParameterName = "@application",
            SqlDbType = SqlDbType.UniqueIdentifier,
            Value = application
          };

          SqlParameter paramId = new SqlParameter
          {
            ParameterName = "@id",
            SqlDbType = SqlDbType.NVarChar,
            Size = 88,
            Value = id
          };

          SqlParameter paramItem = new SqlParameter
          {
            ParameterName = "@item",
            SqlDbType = SqlDbType.Image,
            Value = data
          };

          SqlParameter paramTimeout = new SqlParameter
          {
            ParameterName = "@timeout",
            SqlDbType = SqlDbType.Int,
            Value = sessionState.Timeout
          };

          SqlParameter paramFlags = new SqlParameter
          {
            ParameterName = "@flags",
            SqlDbType = SqlDbType.Int,
            Value = flags
          };

          command.Parameters.Add(paramApplication);
          command.Parameters.Add(paramId);
          command.Parameters.Add(paramItem);
          command.Parameters.Add(paramTimeout);
          command.Parameters.Add(paramFlags);

          using (SqlConnection connection = new SqlConnection(this.m_ConnectionString))
          {
            connection.Open();

            command.Connection = connection;

            command.ExecuteNonQuery();
          }
        }
      }
      catch (SqlException sqlException)
      {
        if (sqlException.Number == 2601 || sqlException.Number == 2627)
        {
          Log.Debug("Attempting to insert a duplicate key into the SQL Session Store. Entry skipped. " + sqlException.Message);
        }
        else
        {
          Log.Error(sqlException.Message, sqlException);
        }        
      }
      catch (Exception ex)
      {
        Log.Error(ex.Message, ex);
      }
    }


    [CanBeNull]
    internal SessionStateStoreData GetItem(Guid application, [NotNull] string id, out SessionStateLockCookie lockCookie, out int flags)
    {
      Debug.ArgumentNotNull(id, "id");

      lockCookie = null;
      flags = 0;

      SessionStateStoreData result = null;

      using (SqlCommand command = new SqlCommand())
      {
        command.CommandText = "[dbo].[GetItem]";
        command.CommandType = CommandType.StoredProcedure;

        SqlParameter paramApplication = new SqlParameter
        {
          ParameterName = "@application",
          SqlDbType = SqlDbType.UniqueIdentifier,
          Value = application
        };

        SqlParameter paramId = new SqlParameter
        {
          ParameterName = "@id",
          SqlDbType = SqlDbType.NVarChar,
          Size = 88,
          Value = id
        };

        SqlParameter paramLocked = new SqlParameter
        {
          ParameterName = "@locked",
          SqlDbType = SqlDbType.Bit,
          Direction = ParameterDirection.Output
        };

        SqlParameter paramLockAcquired = new SqlParameter
        {
          ParameterName = "@lockTimestamp",
          SqlDbType = SqlDbType.DateTime,
          Direction = ParameterDirection.Output
        };

        SqlParameter paramLockCookie = new SqlParameter
        {
          ParameterName = "@lockCookie",
          SqlDbType = SqlDbType.VarChar,
          Size = 32,
          Direction = ParameterDirection.Output
        };

        SqlParameter paramFlags = new SqlParameter
        {
          ParameterName = "@flags",
          SqlDbType = SqlDbType.Int,
          Direction = ParameterDirection.Output
        };

        SqlParameter paramResult = new SqlParameter
        {
          ParameterName = "@result",
          SqlDbType = SqlDbType.Int,
          Direction = ParameterDirection.ReturnValue
        };

        command.Parameters.Add(paramApplication);
        command.Parameters.Add(paramId);
        command.Parameters.Add(paramLocked);
        command.Parameters.Add(paramLockAcquired);
        command.Parameters.Add(paramLockCookie);
        command.Parameters.Add(paramFlags);
        command.Parameters.Add(paramResult);

        int sqlr = 0;
        byte[] buffer = null;

        using (SqlConnection connection = new SqlConnection(this.m_ConnectionString))
        {
          connection.Open();

          command.Connection = connection;

          using (SqlDataReader reader = command.ExecuteReader(CommandBehavior.SingleRow))
          {
            bool received = reader.Read();

            if (received)
            {
              bool hasValue = !reader.IsDBNull(0);

              if (hasValue)
              {
                buffer = ((byte[])reader[0]);
              }
            }
          }

          sqlr = ((int) paramResult.Value);
        }

        if (sqlr == 1)
        {
          flags = ((int) paramFlags.Value);
          bool locked = ((bool) paramLocked.Value);
          string lockId = paramLockCookie.Value.ToString();
          DateTime lockAcquired = (DateTime)paramLockAcquired.Value;
          lockAcquired = DateTime.SpecifyKind(lockAcquired, DateTimeKind.Utc);

          if (locked)
          {
            lockCookie = new SessionStateLockCookie(lockId, lockAcquired);
          }
          else
          {
            Assert.IsNotNull(buffer, "The session item was not returned from the database.");

            result = SessionStateSerializer.Deserialize(buffer);
          }
        }
      }

      return result;
    }



    /// <summary>
    ///   Attempts to lock and load the session state store item with the specified identifier.
    /// </summary>
    /// <param name="application">
    ///   The unique identifier of the application the session state item belongs to.
    /// </param>
    /// <param name="id">
    ///   The unique identifier of the session state store entry to lock.
    /// </param>
    /// <param name="acquiredLockCookie">
    ///   A <see cref="SessionStateLockCookie"/> object that is the lock to set.
    /// </param>
    /// <param name="existingLockCookie">
    ///   ?abc?
    /// </param>
    /// <param name="flags">
    ///   ?abc?
    /// </param>
    /// <returns>
    ///   A <see cref="SessionStateStoreData"/> object loaded from the database if the session state store entry was
    ///   found and successfully locked; otherwise, <c>null</c>.
    /// </returns>
    /// <remarks>
    ///   This method returns <c>null</c> if the session state entry does not exist or if the entry is already locked.
    /// </remarks>
    [CanBeNull]
    internal SessionStateStoreData GetItemExclusive(Guid application, [NotNull] string id, [NotNull] SessionStateLockCookie acquiredLockCookie, out SessionStateLockCookie existingLockCookie, out int flags)
    {
      Debug.ArgumentNotNull(id, "id");
      Debug.ArgumentNotNull(acquiredLockCookie, "acquiredLockCookie");

      flags = 0;
      existingLockCookie = null;

      SessionStateStoreData result = null;

      using (SqlCommand command = new SqlCommand())
      {
        command.CommandText = "[dbo].[GetItemExclusive]";
        command.CommandType = CommandType.StoredProcedure;

        SqlParameter paramApplication = new SqlParameter
        {
          ParameterName = "@application",
          SqlDbType = SqlDbType.UniqueIdentifier,
          Value = application
        };

        SqlParameter paramId = new SqlParameter
        {
          ParameterName = "@id",
          SqlDbType = SqlDbType.NVarChar,
          Size = 88,
          Value = id
        };

        SqlParameter paramLocked = new SqlParameter
        {
          ParameterName = "@locked",
          SqlDbType = SqlDbType.Bit,
          Direction = ParameterDirection.Output
        };

        SqlParameter paramLockTimestamp = new SqlParameter
        {
          ParameterName = "@lockTimestamp",
          SqlDbType = SqlDbType.DateTime,
          Direction = ParameterDirection.Output,
        };

        SqlParameter paramLockCookie = new SqlParameter
        {
          ParameterName = "@lockCookie",
          SqlDbType = SqlDbType.VarChar,
          Size = 32,
          Direction = ParameterDirection.InputOutput,
          Value = acquiredLockCookie.Id
        };

        SqlParameter paramFlags = new SqlParameter
        {
          ParameterName = "@flags",
          SqlDbType = SqlDbType.Int,
          Direction = ParameterDirection.Output
        };

        SqlParameter paramResult = new SqlParameter
        {
          ParameterName = "@result",
          SqlDbType = SqlDbType.Int,
          Direction = ParameterDirection.ReturnValue
        };

        command.Parameters.Add(paramApplication);
        command.Parameters.Add(paramId);
        command.Parameters.Add(paramLocked);
        command.Parameters.Add(paramLockTimestamp);
        command.Parameters.Add(paramLockCookie);
        command.Parameters.Add(paramFlags);
        command.Parameters.Add(paramResult);

        int sqlr = 0;
        byte[] buffer = null;

        using (SqlConnection connection = new SqlConnection(this.m_ConnectionString))
        {
          connection.Open();

          command.Connection = connection;

          using (SqlDataReader reader = command.ExecuteReader(CommandBehavior.SingleRow))
          {
            bool received = reader.Read();

            if (received)
            {
              bool hasValue = !reader.IsDBNull(0);

              if (hasValue)
              {
                buffer = ((byte[])reader[0]);
              }
            }
          }

          sqlr = ((int)paramResult.Value);
        }

        if(sqlr == 1)
        {
          bool isLocked = ((bool) paramLocked.Value);
          flags = ((int)paramFlags.Value);

          if (isLocked)
          {
            string lockId = ((string) paramLockCookie.Value);
            DateTime lockTimestamp = (DateTime)paramLockTimestamp.Value;
            lockTimestamp = DateTime.SpecifyKind(lockTimestamp, DateTimeKind.Utc);

            existingLockCookie = new SessionStateLockCookie(lockId, lockTimestamp);
          }

          if (buffer != null)
          {
            result = SessionStateSerializer.Deserialize(buffer);
          }
        }
      }

      return result;
    }



    /// <summary>
    ///   Updates a locked session state store item and releases the lock.
    /// </summary>
    /// <param name="application">
    ///   The unique identifier of the application the session state item belongs to.
    /// </param>
    /// <param name="id">
    ///   The session state store item to update.
    /// </param>
    /// <param name="lockCookie">
    ///   The identifier of the lock currently held.
    /// </param>
    /// <param name="action">
    ///   A <see cref="SessionStateActions"/> value
    /// </param>
    /// <param name="sessionState">
    ///   A <see cref="SessionStateStoreData"/> that represents the session data.
    /// </param>
    internal void UpdateAndReleaseItem(Guid application, string id, string lockCookie, SessionStateActions action, SessionStateStoreData sessionState)
    {
      Debug.ArgumentNotNull(id, "id");
      Debug.ArgumentNotNull(lockCookie, "lockCookie");
      Debug.ArgumentNotNull(sessionState, "sessionState");

      using (SqlCommand command = new SqlCommand())
      {
        command.CommandText = "[dbo].[SetAndReleaseItem]";
        command.CommandType = CommandType.StoredProcedure;

        SqlParameter paramApplication = new SqlParameter
        {
          ParameterName = "@application",
          SqlDbType = SqlDbType.UniqueIdentifier,
          Value = application
        };

        SqlParameter paramId = new SqlParameter
        {
          ParameterName = "@id",
          SqlDbType = SqlDbType.NVarChar,
          Size = 88,
          Value = id
        };

        SqlParameter paramLockCookie = new SqlParameter
        {
          ParameterName = "@lockCookie",
          SqlDbType = SqlDbType.VarChar,
          Size = 32,
          Value = lockCookie
        };

        SqlParameter paramFlags = new SqlParameter
        {
          ParameterName = "@flags",
          SqlDbType = SqlDbType.Int,
          Value = action
        };

        SqlParameter paramTimeout = new SqlParameter
        {
          ParameterName = "@timeout",
          SqlDbType = SqlDbType.Int,
          Value = sessionState.Timeout
        };

        SqlParameter paramItem = new SqlParameter
        {
          ParameterName = "@item",
          SqlDbType = SqlDbType.Image,
        };

        SqlParameter paramResult = new SqlParameter
        {
          ParameterName = "@result",
          SqlDbType = SqlDbType.Int,
          Direction = ParameterDirection.ReturnValue
        };

        paramItem.Value = SessionStateSerializer.Serialize(sessionState, this.m_Compress);

        command.Parameters.Add(paramApplication);
        command.Parameters.Add(paramId);
        command.Parameters.Add(paramLockCookie);
        command.Parameters.Add(paramFlags);
        command.Parameters.Add(paramTimeout);
        command.Parameters.Add(paramItem);
        command.Parameters.Add(paramResult);

        int sqlr = 0;

        using (SqlConnection connection = new SqlConnection(this.m_ConnectionString))
        {
          connection.Open();

          command.Connection = connection;

          command.ExecuteNonQuery();

          sqlr = ((int)paramResult.Value);
        }

        Debug.Assert(sqlr == 0, "Failed to update and release the session state item.");
      }
    }



    /// <summary>
    ///   Releases a locked session state store item.
    /// </summary>
    /// <param name="application">
    ///   The unique identifier of the application the session state item belongs to.
    /// </param>
    /// <param name="id">
    ///   The session state store item to release.
    /// </param>
    /// <param name="lockCookie">
    ///   The identifier of the lock currently held.
    /// </param>
    internal void ReleaseItem(Guid application, string id, string lockCookie)
    {
      Debug.ArgumentNotNull(id, "id");
      Debug.ArgumentNotNull(lockCookie, "lockCookie");

      using (SqlCommand command = new SqlCommand())
      {
        command.CommandText = "[dbo].[ReleaseItem]";
        command.CommandType = CommandType.StoredProcedure;

        SqlParameter paramApplication = new SqlParameter
        {
          ParameterName = "@application",
          SqlDbType = SqlDbType.UniqueIdentifier,
          Value = application
        };

        SqlParameter paramId = new SqlParameter
        {
          ParameterName = "@id",
          SqlDbType = SqlDbType.NVarChar,
          Size = 88,
          Value = id
        };

        SqlParameter paramLockCookie = new SqlParameter
        {
          ParameterName = "@lockCookie",
          SqlDbType = SqlDbType.VarChar,
          Size = 32,
          Value = lockCookie
        };

        SqlParameter paramResult = new SqlParameter
        {
          ParameterName = "@result",
          SqlDbType = SqlDbType.Int,
          Direction = ParameterDirection.ReturnValue
        };

        command.Parameters.Add(paramApplication);
        command.Parameters.Add(paramId);
        command.Parameters.Add(paramLockCookie);
        command.Parameters.Add(paramResult);

        int sqlr = 0;

        using (SqlConnection connection = new SqlConnection(this.m_ConnectionString))
        {
          connection.Open();

          command.Connection = connection;

          command.ExecuteNonQuery();

          sqlr = ((int)paramResult.Value);
        }

        Debug.Assert(sqlr == 0, "Failed to release the session state item.");
      }
    }



    /// <summary>
    ///   Removes a locked session state store item from the database.
    /// </summary>
    /// <param name="application">
    ///   The unique identifier of the application the session state item belongs to.
    /// </param>
    /// <param name="id">
    ///   The session state store item to release.
    /// </param>
    /// <param name="lockCookie">
    ///   The identifier of the lock currently held.
    /// </param>
    internal void RemoveItem(Guid application, string id, string lockCookie)
    {
      Debug.ArgumentNotNull(id, "id");
      Debug.ArgumentNotNull(lockCookie, "lockCookie");

      using (SqlCommand command = new SqlCommand())
      {
        command.CommandText = "[dbo].[RemoveItem]";
        command.CommandType = CommandType.StoredProcedure;

        SqlParameter paramApplication = new SqlParameter
        {
          ParameterName = "@application",
          SqlDbType = SqlDbType.UniqueIdentifier,
          Value = application
        };

        SqlParameter paramId = new SqlParameter
        {
          ParameterName = "@id",
          SqlDbType = SqlDbType.NVarChar,
          Size = 88,
          Value = id
        };

        SqlParameter paramLockCookie = new SqlParameter
        {
          ParameterName = "@lockCookie",
          SqlDbType = SqlDbType.VarChar,
          Size = 32,
          Value = lockCookie
        };

        SqlParameter paramResult = new SqlParameter
        {
          ParameterName = "@result",
          SqlDbType = SqlDbType.Int,
          Direction = ParameterDirection.ReturnValue
        };

        command.Parameters.Add(paramApplication);
        command.Parameters.Add(paramId);
        command.Parameters.Add(paramLockCookie);
        command.Parameters.Add(paramResult);

        int sqlr = 0;

        using (SqlConnection connection = new SqlConnection(this.m_ConnectionString))
        {
          connection.Open();

          command.Connection = connection;

          command.ExecuteNonQuery();

          sqlr = ((int)paramResult.Value);
        }

        Debug.Assert(sqlr == 0, "Failed to remove the session state item.");
      }
    }



    /// <summary>
    ///   Attempts to lock and load an expired session state store item.
    /// </summary>
    /// <param name="application">
    ///   The unique identifier of the application the session state item belongs to.
    /// </param>
    /// <param name="lockCookie">
    ///   A <see cref="SessionStateLockCookie"/> object that is the lock to set.
    /// </param>
    /// <param name="id">
    ///   A string variable that receives the unique identifier of the expired item.
    /// </param>
    /// <returns>
    ///   A <see cref="SessionStateStoreData"/> object loaded from the database if the session state store entry was
    ///   found and successfully locked; otherwise, <c>null</c>.
    /// </returns>
    /// <remarks>
    ///   This method returns <c>null</c> if the session state entry does not exist or if the entry is already locked.
    /// </remarks>
    internal SessionStateStoreData GetExpiredItemExclusive(Guid application, SessionStateLockCookie lockCookie, out string id)
    {
      Debug.ArgumentNotNull(lockCookie, "lockCookie");

      id = null;

      SessionStateStoreData result = null;

      using (SqlCommand command = new SqlCommand())
      {
        command.CommandText = "[dbo].[GetExpiredItemExclusive]";
        command.CommandType = CommandType.StoredProcedure;

        SqlParameter paramApplication = new SqlParameter
        {
          ParameterName = "@application",
          SqlDbType = SqlDbType.UniqueIdentifier,
          Value = application
        };

        SqlParameter paramId = new SqlParameter
        {
          ParameterName = "@id",
          SqlDbType = SqlDbType.NVarChar,
          Size = 88,
          Direction = ParameterDirection.Output
        };

        SqlParameter paramLockTimestamp = new SqlParameter
        {
          ParameterName = "@lockTimestamp",
          SqlDbType = SqlDbType.DateTime,
          Value = lockCookie.Timestamp
        };

        SqlParameter paramLockCookie = new SqlParameter
        {
          ParameterName = "@lockCookie",
          SqlDbType = SqlDbType.VarChar,
          Size = 32,
          Value = lockCookie.Id
        };

        SqlParameter paramResult = new SqlParameter
        {
          ParameterName = "@result",
          SqlDbType = SqlDbType.Int,
          Direction = ParameterDirection.ReturnValue
        };

        command.Parameters.Add(paramApplication);
        command.Parameters.Add(paramId);
        command.Parameters.Add(paramLockTimestamp);
        command.Parameters.Add(paramLockCookie);
        command.Parameters.Add(paramResult);

        int sqlr = 0;
        byte[] buffer = null;

        using (SqlConnection connection = new SqlConnection(this.m_ConnectionString))
        {
          connection.Open();

          command.Connection = connection;

          using (SqlDataReader reader = command.ExecuteReader(CommandBehavior.SingleRow))
          {
            bool received = reader.Read();

            if (received)
            {
              bool hasValue = !reader.IsDBNull(0);

              if (hasValue)
              {
                buffer = ((byte[])reader[0]);
              }
            }
          }

          sqlr = ((int)paramResult.Value);
        }

        if (sqlr == 1)
        {
          id = ((string) paramId.Value);
          result = SessionStateSerializer.Deserialize(buffer);
        }
      }

      return result;
    }



    internal void UpdateItemExpiration(Guid application, string id)
    {
      Debug.ArgumentNotNull(id, "id");

      using (SqlCommand command = new SqlCommand())
      {
        command.CommandText = "[dbo].[ResetItemTimeout]";
        command.CommandType = CommandType.StoredProcedure;

        SqlParameter paramApplication = new SqlParameter
        {
          ParameterName = "@application",
          SqlDbType = SqlDbType.UniqueIdentifier,
          Value = application
        };

        SqlParameter paramId = new SqlParameter
        {
          ParameterName = "@id",
          SqlDbType = SqlDbType.NVarChar,
          Size = 88,
          Value = id
        };

        command.Parameters.Add(paramApplication);
        command.Parameters.Add(paramId);

        using (SqlConnection connection = new SqlConnection(this.m_ConnectionString))
        {
          connection.Open();

          command.Connection = connection;

          command.ExecuteNonQuery();
        }
      }
    }



    internal Guid GetApplicationIdentifier(string name)
    {
      Debug.ArgumentNotNull(name, "name");

      Guid result;

      using (SqlCommand command = new SqlCommand())
      {
        command.CommandText = "[dbo].[GetApplicationId]";
        command.CommandType = CommandType.StoredProcedure;

        SqlParameter paramName = new SqlParameter
        {
          ParameterName = "@name",
          SqlDbType = SqlDbType.NVarChar,
          Size = 280,
          Value = name
        };

        SqlParameter paramId = new SqlParameter
        {
          ParameterName = "@id",
          SqlDbType = SqlDbType.UniqueIdentifier,
          Direction = ParameterDirection.Output
        };

        command.Parameters.Add(paramName);
        command.Parameters.Add(paramId);

        using (SqlConnection connection = new SqlConnection(this.m_ConnectionString))
        {
          connection.Open();

          command.Connection = connection;

          command.ExecuteNonQuery();
        }

        result = ((Guid) paramId.Value);
      }

      return result;
    }

    /// <summary>
    /// check a id already exists or not
    /// </summary>
    /// <param name="id"></param>
    /// <returns></returns>
    private bool IsItemExist(string id)
    {
      using (SqlCommand sqlCommand = new SqlCommand())
      {
        sqlCommand.CommandText = "SELECT [id] FROM [dbo].[SessionState] WHERE [id]=@id";
        sqlCommand.CommandType = CommandType.Text;
        SqlParameter sqlParameterId = new SqlParameter();
        string paramName = "@id";
        sqlParameterId.ParameterName = paramName;
        sqlParameterId.SqlDbType = SqlDbType.NVarChar;
        sqlParameterId.Size = 88;
        sqlParameterId.Value = id;
        sqlCommand.Parameters.Add(sqlParameterId);
        using (SqlConnection sqlConnection = new SqlConnection(this.m_ConnectionString))
        {
          sqlConnection.Open();
          sqlCommand.Connection = sqlConnection;
          using (SqlDataReader sqlDataReader = sqlCommand.ExecuteReader(CommandBehavior.SingleRow))
          {
            if (sqlDataReader.Read())
            {
              if (!sqlDataReader.IsDBNull(0))
                return true;
            }
          }
        }
      }
      return false;
    }
  }
}
