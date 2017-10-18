using Sitecore.Diagnostics;
using Sitecore.Exceptions;
using Sitecore.SessionProvider.Helpers;
using System;
using System.Collections.Specialized;
using System.Timers;
using System.Web;
using System.Web.SessionState;

namespace Sitecore.Support.SessionProvider
{
  public abstract class SitecoreSessionStateStoreProvider : SessionStateStoreProviderBase
  {
    protected Func<bool> CanStartTimer;

    public bool TriedToStartTimer { get; private set; }

    public bool TimerEnabled
    {
      get
      {
        return this.timer != null && this.timer.Enabled;
      }
    }

    private bool isProcessing;

    private volatile Timer timer;

    private int pollingInterval = 2;

    private SessionStateItemExpireCallback sessionEndCallback;

    private readonly object syncRoot = new object();

    protected SitecoreSessionStateStoreProvider(int pollingInterval) :
      this()
    {
      this.SetPollingInterval(pollingInterval);
    }

    protected SitecoreSessionStateStoreProvider()
    {
      this.timer = new Timer();
      this.timer.AutoReset = true;
      this.timer.Elapsed += this.OnProcessExpiredItems;
      this.TriedToStartTimer = false;
    }

    public void StartTimer()
    {
      if (this.timer != null)
        this.timer.Start();
    }

    public override void Dispose()
    {
      if (null != this.timer)
      {
        this.timer.Stop();
        this.timer.Dispose();
        this.timer = null;
      }
    }

    public override void Initialize(string name, NameValueCollection config)
    {
      base.Initialize(name, config);

      var configuration = new ConfigReader(config, name);

      this.SetPollingInterval(configuration.GetInt32("pollingInterval", this.pollingInterval));
    }

    public override bool SetItemExpireCallback(SessionStateItemExpireCallback expireCallback)
    {
      this.sessionEndCallback = expireCallback;

      if (expireCallback != null)
      {
        this.TriedToStartTimer = true;
        if (this.CanStartTimer == null || this.CanStartTimer())
        {
          this.StartTimer();
        }
        return true;
      }

      this.timer.Stop();
      return false;
    }

    public override SessionStateStoreData CreateNewStoreData(HttpContext context, int timeout)
    {
      if (context == null)
      {
        throw new ArgumentNullException("context");
      }

      HttpStaticObjectsCollection staticObjects = SessionStateUtility.GetSessionStaticObjects(context);
      var sessionItems = new SessionStateItemCollection();

      var result = new SessionStateStoreData(sessionItems, staticObjects, timeout);

      return result;
    }

    public override void EndRequest(HttpContext context)
    {
    }

    public override void InitializeRequest(HttpContext context)
    {
    }

    protected abstract SessionStateStoreData GetExpiredItemExclusive(DateTime signalTime, SessionStateLockCookie lockCookie, out string id);

    protected virtual string OnProcessExpiredItems(DateTime signalTime)
    {
      string id;

      SessionStateLockCookie cookie = SessionStateLockCookie.Generate(signalTime);
      SessionStateStoreData item = this.GetExpiredItemExclusive(signalTime, cookie, out id);

      if (item != null)
      {
        try
        {
          this.ExecuteSessionEnd(id, item);
        }
        finally
        {
          this.RemoveItem(id, cookie.Id);
        }
      }

      return id;
    }

    protected void ExecuteSessionEnd(string id, SessionStateStoreData item)
    {
      if (id == null)
      {
        throw new ArgumentNullException("id");
      }

      if (item == null)
      {
        throw new ArgumentNullException("item");
      }

      if (null != this.sessionEndCallback)
      {
        try
        {
          this.sessionEndCallback.Invoke(id, item);
        }
        catch (Exception ex)
        {
          Log.Error("Error executing the session end callback. Id: " + id, ex, this);
        }
      }
    }

    protected abstract void RemoveItem(string id, string lockCookie);

    private void OnProcessExpiredItems([NotNull] object sender, [NotNull] ElapsedEventArgs args)
    {
      Debug.ArgumentNotNull(sender, "sender");
      Debug.ArgumentNotNull(args, "args");

      lock (this.syncRoot)
      {
        if (this.isProcessing)
        {
          return;
        }

        this.isProcessing = true;
      }

      try
      {
        if (this.timer == null)
        {
          // the provider instance is being disposed, so we abort. Also below.
          return;
        }

        bool found;

        do
        {
          DateTime signalTime = args.SignalTime.ToUniversalTime();
          found = this.OnProcessExpiredItems(signalTime) != null;
        }
        while ((this.timer != null) && found);
      }
      catch (Exception)
      {
        Log.SingleError("Failed processing expired items. These will be retried according to the pollingInterval.", this);
        throw;
      }
      finally
      {
        lock (this.syncRoot)
        {
          this.isProcessing = false;
        }
      }
    }

    private void SetPollingInterval(int interval)
    {
      if (interval < 1)
      {
        throw new ConfigurationException("The polling interval specified is too small.");
      }

      if (interval > 120)
      {
        throw new ConfigurationException("The polling interval specified is too high.");
      }

      this.pollingInterval = interval;

      this.timer.Interval = 1000d * this.pollingInterval;
    }
  }
}