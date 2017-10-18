
using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Web;
using System.Web.SessionState;

namespace Sitecore.Support.SessionProvider
{
  public static class SessionStateSerializer
  {
    [Flags]
    private enum ContentFlags : byte
    {
      None = 0x00,
      HasStaticObjects = 0x01,
      HasSessionItems = 0x02
    }

    public static byte[] Serialize(SessionStateStoreData sessionState, bool compress)
    {
      if (sessionState == null)
      {
        throw new ArgumentNullException("sessionState");
      }

      byte[] result;

      using (MemoryStream stream = new MemoryStream())
      {
        using (BinaryWriter writer = new BinaryWriter(stream))
        {
          writer.Write(compress);

          if (compress)
          {
            byte[] compressed = Compress(sessionState);

            writer.Write(compressed.Length);
            writer.Write(compressed);
          }
          else
          {
            Serialize(writer, sessionState);
          }
        }

        result = stream.ToArray();
      }

      return result;
    }

    public static SessionStateStoreData Deserialize(byte[] data)
    {
      Debug.Assert(null != data);

      SessionStateStoreData result;

      using (MemoryStream stream = new MemoryStream(data))
      {
        using (BinaryReader reader = new BinaryReader(stream))
        {
          bool decompress = reader.ReadBoolean();

          if (decompress)
          {
            int length = reader.ReadInt32();
            byte[] compressed = reader.ReadBytes(length);

            result = Decompress(compressed);
          }
          else
          {
            result = Deserialize(reader);
          }
        }
      }

      return result;
    }

    private static byte[] Compress(SessionStateStoreData item)
    {
      Debug.Assert(null != item);

      byte[] result;

      using (MemoryStream memoryStream = new MemoryStream())
      {
        using (BinaryWriter writer = new BinaryWriter(memoryStream))
        {
          Serialize(writer, item);
        }

        result = memoryStream.ToArray();
      }

      using (MemoryStream memoryStream = new MemoryStream())
      {
        using (DeflateStream deflateStream = new DeflateStream(memoryStream, CompressionMode.Compress))
        {
          using (BinaryWriter writer = new BinaryWriter(deflateStream))
          {
            writer.Write(result);
          }
        }

        result = memoryStream.ToArray();
      }

      return result;
    }

    private static SessionStateStoreData Decompress(byte[] data)
    {
      Debug.Assert(null != data);

      SessionStateStoreData result;

      using (MemoryStream memoryStream = new MemoryStream(data))
      {
        using (DeflateStream deflateStream = new DeflateStream(memoryStream, CompressionMode.Decompress))
        {
          using (BinaryReader reader = new BinaryReader(deflateStream))
          {
            result = Deserialize(reader);
          }
        }
      }

      return result;
    }

    private static void Serialize(BinaryWriter writer, SessionStateStoreData item)
    {
      Debug.Assert(null != writer);
      Debug.Assert(null != item);

      HttpStaticObjectsCollection staticObjects = item.StaticObjects;
      SessionStateItemCollection sessionItems = (item.Items as SessionStateItemCollection);

      ContentFlags flags = ContentFlags.None;

      if ((null != staticObjects) && (false == staticObjects.NeverAccessed))
      {
        flags |= ContentFlags.HasStaticObjects;
      }

      if ((null != sessionItems) && (0 < sessionItems.Count))
      {
        flags |= ContentFlags.HasSessionItems;
      }

      writer.Write((byte)flags);
      writer.Write(item.Timeout);

      if (ContentFlags.HasStaticObjects == (ContentFlags.HasStaticObjects & flags))
      {
        staticObjects.Serialize(writer);
      }

      if (ContentFlags.HasSessionItems == (ContentFlags.HasSessionItems & flags))
      {
        sessionItems.Serialize(writer);
      }
    }

    private static SessionStateStoreData Deserialize(BinaryReader reader)
    {
      Debug.Assert(null != reader);

      ContentFlags flags = (ContentFlags)reader.ReadByte();

      int timeout = reader.ReadInt32();

      HttpStaticObjectsCollection staticItems;
      SessionStateItemCollection dynamicItems;

      if (ContentFlags.HasStaticObjects == (ContentFlags.HasStaticObjects & flags))
      {
        staticItems = HttpStaticObjectsCollection.Deserialize(reader);
      }
      else
      {
        staticItems = new HttpStaticObjectsCollection();
      }

      if (ContentFlags.HasSessionItems == (ContentFlags.HasSessionItems & flags))
      {
        dynamicItems = SessionStateItemCollection.Deserialize(reader);
      }
      else
      {
        dynamicItems = new SessionStateItemCollection();
      }

      SessionStateStoreData result = new SessionStateStoreData(dynamicItems, staticItems, timeout);

      return result;
    }
  }
}
