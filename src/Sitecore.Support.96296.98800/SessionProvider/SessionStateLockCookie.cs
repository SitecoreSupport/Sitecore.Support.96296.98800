
using System;

namespace Sitecore.Support.SessionProvider
{
  public sealed class SessionStateLockCookie
  {
    public static readonly SessionStateLockCookie Empty = new SessionStateLockCookie(string.Empty, new DateTime(0L, DateTimeKind.Utc));

    private readonly string m_Id;

    private readonly DateTime m_Timestamp;

    public string Id
    {
      get
      {
        string result = this.m_Id;

        bool isEmpty = string.IsNullOrWhiteSpace(result);

        if (isEmpty)
        {
          result = string.Empty;
        }

        return result;
      }
    }

    public DateTime Timestamp
    {
      get
      {
        DateTime result = DateTime.MinValue.ToUniversalTime();

        if (this.IsLocked)
        {
          result = this.m_Timestamp;
        }

        return result;
      }
    }

    public bool IsLocked
    {
      get
      {
        return (string.Empty != this.Id);
      }
    }

    public SessionStateLockCookie(string id, DateTime timestamp)
    {
      this.m_Id = id;
      this.m_Timestamp = DateUtil.ToUniversalTime(timestamp);
    }

    public static SessionStateLockCookie Generate(DateTime timestamp)
    {
      string id = GenerateUniqueId();
      SessionStateLockCookie result = new SessionStateLockCookie(id, timestamp);

      return result;
    }

    private static string GenerateUniqueId()
    {
      Guid value = Guid.NewGuid();
      string result = value.ToString("N");

      result = result.ToLowerInvariant();

      return result;
    }
  }
}
