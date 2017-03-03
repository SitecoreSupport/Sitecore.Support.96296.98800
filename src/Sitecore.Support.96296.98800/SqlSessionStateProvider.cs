using Sitecore.Diagnostics;
using Sitecore.SessionProvider.Helpers;
using System;
using System.Collections.Specialized;
using System.Configuration;
using System.Diagnostics;
using System.Reflection;
using System.Timers;
using System.Web;
using System.Web.SessionState;
using Sitecore.SessionProvider;
using System.Threading.Tasks;

namespace Sitecore.Support.SessionProvider.Sql
{
    public class SqlSessionStateProvider : SitecoreSessionStateStoreProvider
    {
        private Guid m_ApplicationId;

        private SqlSessionStateStore m_Store;

        private static readonly object SyncRoot;

        private static readonly FieldInfo timerInfo;

        private static readonly MethodInfo methodInfo;

        private Guid ApplicationId
        {
            get
            {
                return this.m_ApplicationId;
            }
        }

        private SqlSessionStateStore Store
        {
            get
            {
                return this.m_Store;
            }
        }

        static SqlSessionStateProvider()
        {
            Trace.WriteLine("SQL Session State Provider is initializing.", "SqlSessionStateProvider");

            SqlSessionStateProvider.SyncRoot = new object();
            SqlSessionStateProvider.timerInfo = typeof(SitecoreSessionStateStoreProvider).GetField("timer", BindingFlags.Instance | BindingFlags.NonPublic);
            SqlSessionStateProvider.methodInfo = typeof(SitecoreSessionStateStoreProvider).GetMethod("OnProcessExpiredItems", BindingFlags.Instance | BindingFlags.NonPublic, null, new Type[]
            {
                typeof(object),
                typeof(ElapsedEventArgs)
            }, null);
        }

        public SqlSessionStateProvider()
        {
            if (SqlSessionStateProvider.timerInfo != null && SqlSessionStateProvider.methodInfo != null)
            {
                Timer timer = (Timer)SqlSessionStateProvider.timerInfo.GetValue(this);
                Delegate @delegate = Delegate.CreateDelegate(typeof(ElapsedEventHandler), this, SqlSessionStateProvider.methodInfo);
                timer.Elapsed -= (ElapsedEventHandler)@delegate;
                timer.Elapsed += new ElapsedEventHandler(this.OnProcessExpiredItems);
            }
        }

        private void OnProcessExpiredItems(object sender, ElapsedEventArgs args)
        {
            Task.Factory.StartNew(delegate
            {
                lock (SqlSessionStateProvider.SyncRoot)
                {
                    try
                    {
                        if ((Timer)SqlSessionStateProvider.timerInfo.GetValue(this) != null)
                        {
                            bool flag;
                            do
                            {
                                DateTime dateTime = args.SignalTime.ToUniversalTime();
                                flag = (this.OnProcessExpiredItems(dateTime) != null);
                            }
                            while ((Timer)SqlSessionStateProvider.timerInfo.GetValue(this) != null & flag);
                        }
                    }
                    catch (Exception)
                    {
                        Log.SingleError("Failed processing expired items. These will be retried according to the pollingInterval.", this);
                        throw;
                    }
                }
            });
        }

        public override void CreateUninitializedItem(HttpContext context, string id, int timeout)
        {
            Assert.ArgumentNotNull(context, "context");
            Assert.ArgumentNotNull(id, "id");
            SessionStateItemCollection sessionItems = new SessionStateItemCollection();
            HttpStaticObjectsCollection staticObjects = new HttpStaticObjectsCollection();
            SessionStateStoreData sessionState = new SessionStateStoreData(sessionItems, staticObjects, timeout);
            this.m_Store.InsertItem(this.ApplicationId, id, 1, sessionState);
        }

        public override SessionStateStoreData GetItem(HttpContext context, string id, out bool locked, out TimeSpan lockAge, out object lockId, out SessionStateActions actions)
        {
            Assert.ArgumentNotNull(context, "context");
            Assert.ArgumentNotNull(id, "id");
            locked = false;
            lockAge = TimeSpan.Zero;
            lockId = null;
            actions = SessionStateActions.None;
            int num = 0;
            SessionStateLockCookie sessionStateLockCookie = null;
            SessionStateStoreData item = this.Store.GetItem(this.ApplicationId, id, out sessionStateLockCookie, out num);
            actions = (SessionStateActions)num;
            if (sessionStateLockCookie != null)
            {
                locked = true;
                lockId = sessionStateLockCookie.Id;
                lockAge = DateTime.UtcNow - sessionStateLockCookie.Timestamp;
            }
            return item;
        }

        public override SessionStateStoreData GetItemExclusive(HttpContext context, string id, out bool locked, out TimeSpan lockAge, out object lockId, out SessionStateActions actions)
        {
            Assert.ArgumentNotNull(context, "context");
            Assert.ArgumentNotNull(id, "id");
            lockAge = TimeSpan.Zero;
            actions = SessionStateActions.None;
            int num = 0;
            SessionStateLockCookie sessionStateLockCookie = null;
            SessionStateLockCookie sessionStateLockCookie2 = SessionStateLockCookie.Generate(DateTime.UtcNow);
            SessionStateStoreData itemExclusive = this.Store.GetItemExclusive(this.ApplicationId, id, sessionStateLockCookie2, out sessionStateLockCookie, out num);
            if (sessionStateLockCookie != null)
            {
                locked = true;
                lockAge = DateTime.UtcNow - sessionStateLockCookie.Timestamp;
                lockId = sessionStateLockCookie.Id;
            }
            else
            {
                locked = false;
                lockId = sessionStateLockCookie2.Id;
                actions = (SessionStateActions)num;
            }
            return itemExclusive;
        }

        public override void Initialize(string name, NameValueCollection config)
        {
            Assert.ArgumentNotNull(name, "name");
            Assert.ArgumentNotNull(config, "config");
            base.Initialize(name, config);
            ConfigReader configReader = new ConfigReader(config, name);
            string @string = configReader.GetString("sessionType", true);
            string string2 = configReader.GetString("connectionStringName", false);
            string connectionString = ConfigurationManager.ConnectionStrings[string2].ConnectionString;
            bool @bool = configReader.GetBool("compression", false);
            this.m_Store = new SqlSessionStateStore(connectionString, @bool);
            this.m_ApplicationId = this.m_Store.GetApplicationIdentifier(@string);
        }

        public override void ReleaseItemExclusive(HttpContext context, string id, object lockId)
        {
            Assert.ArgumentNotNull(context, "context");
            Assert.ArgumentNotNull(id, "id");
            Assert.ArgumentNotNull(lockId, "lockId");
            string lockCookie = System.Convert.ToString(lockId);
            this.Store.ReleaseItem(this.ApplicationId, id, lockCookie);
        }

        public override void RemoveItem(HttpContext context, string id, object lockId, SessionStateStoreData item)
        {
            Assert.ArgumentNotNull(context, "context");
            Assert.ArgumentNotNull(id, "id");
            Assert.ArgumentNotNull(lockId, "lockId");
            string lockCookie = System.Convert.ToString(lockId);
            try
            {
                base.ExecuteSessionEnd(id, item);
            }
            finally
            {
                this.Store.RemoveItem(this.ApplicationId, id, lockCookie);
            }
        }

        public override void ResetItemTimeout(HttpContext context, string id)
        {
            Assert.ArgumentNotNull(context, "context");
            Assert.ArgumentNotNull(id, "id");
            this.Store.UpdateItemExpiration(this.ApplicationId, id);
        }

        public override void SetAndReleaseItemExclusive(HttpContext context, string id, SessionStateStoreData sessionState, object lockId, bool newItem)
        {
            Assert.ArgumentNotNull(context, "context");
            Assert.ArgumentNotNull(id, "id");
            if (newItem)
            {
                this.Store.InsertItem(this.ApplicationId, id, 0, sessionState);
                return;
            }
            string lockCookie = System.Convert.ToString(lockId);
            this.Store.UpdateAndReleaseItem(this.ApplicationId, id, lockCookie, SessionStateActions.None, sessionState);
        }

        protected override SessionStateStoreData GetExpiredItemExclusive(DateTime signalTime, SessionStateLockCookie lockCookie, out string id)
        {
            return this.Store.GetExpiredItemExclusive(this.ApplicationId, lockCookie, out id);
        }

        protected override void RemoveItem(string id, string lockCookie)
        {
            this.Store.RemoveItem(this.ApplicationId, id, lockCookie);
        }
    }
}