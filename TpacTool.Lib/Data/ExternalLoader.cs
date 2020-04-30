﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using JetBrains.Annotations;
using LZ4;

namespace TpacTool.Lib
{
	public sealed class ExternalLoader<T> : AbstractExternalLoader where T : ExternalData, new()
	{
#if NET40
		private volatile WeakReference _data;
		private T _strongRef;
#else
		private WeakReference<T> _data;
		private T _strongRef;
#endif

		internal ExternalLoader([NotNull] FileInfo file)
		{
			_file = file;
			if (!_file.Exists)
				throw new FileNotFoundException("Cannot find file: " + _file.FullName);
			OwnerGuid = Guid.Empty;
		}

		public ExternalLoader() : this(new T())
		{
		}

		public ExternalLoader([NotNull] T data)
		{
#if NET40
			_data = new WeakReference(data);
			_strongRef = data;
#else
			_data = new WeakReference<T>(data);
			_strongRef = data;
#endif
			_file = null;
			OwnerGuid = Guid.NewGuid();
			if (data.TypeGuid == Guid.Empty)
			{
				throw new ArgumentException("The data which were assigned to the loader must have a type guid.");
			}
		}

		private byte[] lz4decompress(byte[] input, int outLength)
		{
			return LZ4Codec.Decode(input, 0, input.Length, outLength);
		}

		private byte[] lz4compress(byte[] input)
		{
			return LZ4Codec.EncodeHC(input, 0, input.Length);
		}

		public override bool IsDataLoaded() // TODO:
		{
#if NET40
			return _strongRef != null || (_data != null && _data.IsAlive);
#else
			return _strongRef != null || (_data != null && _data.TryGetTarget(out var dontcare));
#endif
		}

		internal protected override void ForceLoad()
		{
			var forceLoad = Data;
		}

		internal protected override void ForceLoad(BinaryReader fullStream)
		{
#if NET40
			_data = new WeakReference(ReadData(fullStream));
#else
			_data = new WeakReference<T>(ReadData(fullStream));
#endif
		}

		public override void MarkLongLive()
		{
			_strongRef = Data;
		}

		private T ReadData()
		{
			using (var stream = _file.OpenBinaryReader())
			{
				return ReadData(stream);
			}
		}

		private T ReadData(BinaryReader fullStream)
		{
			byte[] rawData = GetRawData(fullStream);
			T data = new T();
			using (var stream = rawData.CreateBinaryReader())
			{
				stream.RecordPosition();
				data.ReadData(stream, _userdata.IsValueCreated ? _userdata.Value : EMPTY_USERDATA, (int)_actualSize);
				stream.AssertLength((long)_actualSize);
			}

			data.TypeGuid = TypeGuid;

			return data;
		}

		private byte[] GetRawData(BinaryReader stream)
		{
			byte[] rawData = null;
			if (!stream.BaseStream.CanSeek)
				throw new IOException("The base stream must support random access (seek)");
			stream.BaseStream.Seek((long)_offset, SeekOrigin.Begin);
			switch (_storageFormat)
			{
				case StorageFormat.Uncompressed:
					{
						rawData = stream.ReadBytes((int)_storageSize);
						break;
					}
				case StorageFormat.LZ4HC:
					{
						rawData = stream.ReadBytes((int)_storageSize);
						rawData = lz4decompress(rawData, (int)_actualSize);
						break;
					}
				default:
					throw new ArgumentException("Unsupported data storage format: " + _storageFormat);
			}
			return rawData;
		}

#if NET40
		public T Data
		{
			get
			{
				if (_strongRef != null)
					return _strongRef;
				T loadedData = null;
				if (_data == null || (loadedData = _data.Target as T) == null)
				{
					lock (_file)
					{
						if (_data == null || (loadedData = _data.Target as T) == null)
						{
							loadedData = ReadData();
							_data = new WeakReference(loadedData);
						}
					}
				}
				return loadedData;
			}
			set
			{
				_data = new WeakReference(value);
			}
		}
#else
		public T Data
		{
			get
			{
				if (_strongRef != null)
					return _strongRef;
				if (_data == null || !_data.TryGetTarget(out T loadedData))
				{
					lock (_file)
					{
						var weakRef = Volatile.Read(ref _data);
						if (weakRef == null || !weakRef.TryGetTarget(out loadedData))
						{
							loadedData = ReadData();
							Volatile.Write(ref _data, new WeakReference<T>(loadedData));
						}
					}
				}
				return loadedData;
			}
			set
			{
				Volatile.Write(ref _data, new WeakReference<T>(value));
			}
		}
#endif
	}
}