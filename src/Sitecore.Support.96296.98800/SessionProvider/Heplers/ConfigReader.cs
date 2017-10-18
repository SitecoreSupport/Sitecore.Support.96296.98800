using System;
using System.Collections.Specialized;
using System.Configuration;
using System.Globalization;
using System.Web.Configuration;
using System.Web.Hosting;

namespace Sitecore.Support.SessionProvider.Helpers
{
  public class ConfigReader 
  {
    protected string OwnerName { get; set; }

    protected NameValueCollection Values { get; set; }

    public ConfigReader(NameValueCollection values, string ownerName)
    {
      this.Values = values;
      this.OwnerName = ownerName;
    }

    public virtual bool GetBool(string key, bool defaultValue)
    {
      var value = this.GetString(key, null, false);

      if (value == null)
      {
        return defaultValue;
      }

      return value.Equals("true", StringComparison.InvariantCultureIgnoreCase) || value == "1";
    }

    public virtual int GetInt32(string key, int defaultValue)
    {
      string value = this.GetString(key, null, false);

      if (value == null)
      {
        return defaultValue;
      }

      int result = 0;

      try
      {
        result = int.Parse(value, CultureInfo.InvariantCulture);
      }
      catch (FormatException ex)
      {
        throw new Sitecore.Exceptions.ConfigurationException("The specified value is not a valid integer.", ex);
      }
      catch (OverflowException ex)
      {
        throw new Sitecore.Exceptions.ConfigurationException("The specified value is too large or too small.", ex);
      }

      return result;
    }

    public virtual TSection GetConfigSection<TSection>(string sectionPath) where TSection : class
    {
      var configuration = WebConfigurationManager.OpenWebConfiguration(HostingEnvironment.ApplicationVirtualPath);

      var section = configuration.GetSection(sectionPath);

      if (section == null)
      {
        string message = string.Format(CultureInfo.InvariantCulture, "Could not get the configuration section '{0}'.", sectionPath);
        throw new ConfigurationErrorsException(message);
      }

      var typedSection = section as TSection;

      if (typedSection == null)
      {
        string message = string.Format(CultureInfo.InvariantCulture, "Could not cast the configuration section '{0}' to '{1}'.", sectionPath, typeof(TSection).FullName);
        throw new ConfigurationErrorsException(message);
      }

      return typedSection;
    }

    public virtual string GetConnectionString()
    {
      return this.GetConnectionString("connectionStringName");
    }

    public virtual string GetConnectionString(string connectionNameKeyName)
    {
      var name = this.GetString(connectionNameKeyName, false);

      var settings = ConfigurationManager.ConnectionStrings[name];

      if (settings == null || string.IsNullOrWhiteSpace(settings.ConnectionString))
      {
        string message = string.Format(CultureInfo.InvariantCulture, "Could not retrieve connection string named '{0}' from configuration. Connection string name retrieved from configuration of '{1}'", name, this.OwnerName);
        throw new ConfigurationErrorsException(message);
      }

      return settings.ConnectionString;
    }

    public virtual string GetString(string key, bool allowEmpty)
    {
      var value = this.Values[key];

      if (value == null || (!allowEmpty && string.IsNullOrWhiteSpace(value)))
      {
        this.ThrowMissingValue(key);
      }
      else
      {
        value = value.Trim();
      }

      return value;
    }

    public virtual string GetString(string key, string defaultValue, bool allowEmpty)
    {
      var value = this.Values[key];

      if (value == null)
      {
        return defaultValue;
      }

      if (string.IsNullOrWhiteSpace(value))
      {
        return allowEmpty ? value : defaultValue;
      }

      return value;
    }

    public virtual TimeSpan GetTimeSpan(string key, TimeSpan defaultValue)
    {
      var value = this.GetString(key, null, false);

      if (value == null)
      {
        return defaultValue;
      }

      TimeSpan parsedValue;

      if (TimeSpan.TryParse(value, out parsedValue))
      {
        return parsedValue;
      }

      return defaultValue;
    }

    public virtual TValue GetTypedValue<TSection, TValue>(string sectionPath, Func<TSection, TValue> mapper) where TSection : class
    {
      var section = this.GetConfigSection<TSection>(sectionPath);

      return mapper(section);
    }

    protected virtual void ThrowMissingValue(string key)
    {
      string message = string.Format(CultureInfo.InvariantCulture, "A required value was not provided in the configuration of '{0}'. Key: {1}", this.OwnerName, key);
      throw new ConfigurationErrorsException(message);
    }
  }
}