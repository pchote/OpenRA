#region Copyright & License Information
/*
 * Copyright (c) The OpenRA Developers and Contributors
 * This file is part of OpenRA, which is free software. It is made
 * available to you under the terms of the GNU General Public License
 * as published by the Free Software Foundation, either version 3 of
 * the License, or (at your option) any later version. For more
 * information, see COPYING.
 */
#endregion

using System;
using System.Collections.Generic;
using System.IO;
using OpenRA.Primitives;

namespace OpenRA.Mods.Cnc.AudioLoaders
{
	public class VocLoader : ISoundLoader
	{
		bool ISoundLoader.TryParseSound(Stream stream, out ISoundFormat sound)
		{
			try
			{
				sound = new VocFormat(stream);
				return true;
			}
			catch
			{
				// Not a (supported) VOC
			}

			sound = null;
			return false;
		}
	}

	public sealed class VocFormat : ISoundFormat
	{
		public int SampleBits => 8;
		public int Channels => 1;
		public int SampleRate { get; }
		public float LengthInSeconds => (float)totalSamples / SampleRate;
		public Stream GetPCMInputStream() { return new VocStream(new VocFormat(this)); }
		public void Dispose() { stream.Dispose(); }

		readonly byte[] buffer = new byte[4096];
		readonly Stream stream;
		readonly VocBlock[] blocks;
		readonly int totalSamples;

		IEnumerator<VocBlock> currentBlock;
		bool currentBlockEnded;
		int samplesLeftInBlock;
		int samplePosition;

		struct VocFileHeader
		{
			public string Description;
			public int DatablockOffset;
			public int Version;
			public int ID;

			public static VocFileHeader Read(Stream s)
			{
				VocFileHeader vfh;
				vfh.Description = s.ReadASCII(20);
				vfh.DatablockOffset = s.ReadUInt16();
				vfh.Version = s.ReadUInt16();
				vfh.ID = s.ReadUInt16();
				return vfh;
			}
		}

		struct VocBlock
		{
			public int Code;
			public int Length;
			public VocSampleBlock SampleBlock;
			public VocLoopBlock LoopBlock;
		}

		struct VocSampleBlock
		{
			public int Rate;
			public int Samples;
			public long Offset;
		}

		struct VocLoopBlock
		{
			public int Count;
		}

		public VocFormat(Stream stream)
		{
			this.stream = stream;

			CheckVocHeader(stream);
			Preload(stream, out blocks, out totalSamples, out var sampleRate);
			SampleRate = sampleRate;
			Rewind();
		}

		VocFormat(VocFormat cloneFrom)
		{
			SampleRate = cloneFrom.SampleRate;
			stream = SegmentStream.CreateWithoutOwningStream(cloneFrom.stream, 0, (int)cloneFrom.stream.Length);
			blocks = cloneFrom.blocks;
			totalSamples = cloneFrom.totalSamples;
			Rewind();
		}

		static void CheckVocHeader(Stream stream)
		{
			var vfh = VocFileHeader.Read(stream);

			if (!vfh.Description.StartsWith("Creative Voice File", StringComparison.Ordinal))
				throw new InvalidDataException("Voc header description not recognized");
			if (vfh.DatablockOffset != 26)
				throw new InvalidDataException("Voc header offset is wrong");
			if (vfh.Version < 0x0100 || vfh.Version >= 0x0200)
				throw new InvalidDataException("Voc header version " + vfh.Version.ToStringInvariant("X") + " not supported");
			if (vfh.ID != ~vfh.Version + 0x1234)
				throw new InvalidDataException("Voc header id is bogus - expected: " +
					(~vfh.Version + 0x1234).ToStringInvariant("X") + " but value is : " + vfh.ID.ToStringInvariant("X"));
		}

		static int GetSampleRateFromVocRate(int vocSampleRate)
		{
			if (vocSampleRate == 256)
				throw new InvalidDataException("Invalid frequency divisor 256 in voc file");
			if (vocSampleRate == 0xa5 || vocSampleRate == 0xa6)
				return 11025;
			else if (vocSampleRate == 0xd2 || vocSampleRate == 0xd3)
				return 22050;
			else
				return (int)(1000000L / (256L - vocSampleRate));
		}

		static void Preload(Stream stream, out VocBlock[] blocks, out int totalSamples, out int sampleRate)
		{
			var blockList = new List<VocBlock>();
			totalSamples = 0;
			sampleRate = 0;

			while (true)
			{
				var block = default(VocBlock);
				try
				{
					block.Code = stream.ReadUInt8();
					block.Length = 0;
				}
				catch (EndOfStreamException)
				{
					// Stream is allowed to end without a last block
					break;
				}

				if (block.Code == 0 || block.Code > 9)
					break;

				block.Length = stream.ReadUInt8();
				block.Length |= stream.ReadUInt8() << 8;
				block.Length |= stream.ReadUInt8() << 16;

				var skip = 0;
				switch (block.Code)
				{
					// Sound data
					case 1:
					{
						if (block.Length < 2)
							throw new InvalidDataException("Invalid sound data block length in voc file");
						var freqDiv = stream.ReadUInt8();
						block.SampleBlock.Rate = GetSampleRateFromVocRate(freqDiv);
						var codec = stream.ReadUInt8();
						if (codec != 0)
							throw new InvalidDataException("Unhandled codec used in voc file");
						skip = block.Length - 2;
						block.SampleBlock.Samples = skip;
						block.SampleBlock.Offset = stream.Position;

						// See if last block contained additional information
						if (blockList.Count > 0)
						{
							var b = blockList[^1];
							if (b.Code == 8)
							{
								block.SampleBlock.Rate = b.SampleBlock.Rate;
								blockList.Remove(b);
							}
						}

						sampleRate = Math.Max(sampleRate, block.SampleBlock.Rate);
						break;
					}

					// Silence
					case 3:
					{
						if (block.Length != 3)
							throw new InvalidDataException("Invalid silence block length in voc file");
						block.SampleBlock.Offset = 0;
						block.SampleBlock.Samples = stream.ReadUInt16() + 1;
						var freqDiv = stream.ReadUInt8();
						block.SampleBlock.Rate = GetSampleRateFromVocRate(freqDiv);
						break;
					}

					// Repeat start
					case 6:
					{
						if (block.Length != 2)
							throw new InvalidDataException("Invalid repeat start block length in voc file");
						block.LoopBlock.Count = stream.ReadUInt16() + 1;
						break;
					}

					// Repeat end
					case 7:
						break;

					// Extra info
					case 8:
					{
						if (block.Length != 4)
							throw new InvalidDataException("Invalid info block length in voc file");
						int freqDiv = stream.ReadUInt16();
						if (freqDiv == 65536)
							throw new InvalidDataException("Invalid frequency divisor 65536 in voc file");
						var codec = stream.ReadUInt8();
						if (codec != 0)
							throw new InvalidDataException("Unhandled codec used in voc file");
						var channels = stream.ReadUInt8() + 1;
						if (channels != 1)
							throw new InvalidDataException("Unhandled number of channels in voc file");
						block.SampleBlock.Offset = 0;
						block.SampleBlock.Samples = 0;
						block.SampleBlock.Rate = (int)(256000000L / (65536L - freqDiv));
						break;
					}

					// Sound data (New format)
					case 9:
					default:
						throw new InvalidDataException("Unhandled code in voc file");
				}

				if (skip > 0)
					stream.Seek(skip, SeekOrigin.Current);
				blockList.Add(block);
			}

			// Check validity and calculated total number of samples
			foreach (var b in blockList)
			{
				if (b.Code == 8)
					throw new InvalidDataException("Unused block 8 in voc file");
				if (b.Code != 1 && b.Code != 9)
					continue;
				if (b.SampleBlock.Rate != sampleRate)
					throw new InvalidDataException("Voc file contains chunks with different sample rate");
				totalSamples += b.SampleBlock.Samples;
			}

			blocks = blockList.ToArray();
		}

		void Rewind()
		{
			currentBlock = ((IEnumerable<VocBlock>)blocks).GetEnumerator();
			currentBlockEnded = false;
			samplesLeftInBlock = 0;
			samplePosition = 0;

			while (currentBlock.MoveNext())
			{
				if (currentBlock.Current.Code == 1)
				{
					stream.Seek(currentBlock.Current.SampleBlock.Offset, SeekOrigin.Begin);
					samplesLeftInBlock = currentBlock.Current.SampleBlock.Samples;
					return;
				}
			}

			currentBlockEnded = true;
		}

		bool EndOfData => currentBlockEnded && samplesLeftInBlock == 0;

		int FillBuffer(int maxSamples)
		{
			var bufferedSamples = 0;
			var offset = 0;

			maxSamples = Math.Min(buffer.Length, maxSamples);

			while (maxSamples > 0 && !EndOfData)
			{
				var len = Math.Min(maxSamples, samplesLeftInBlock);
				stream.ReadBytes(buffer, offset, len);
				offset += len;
				var samplesRead = len;
				bufferedSamples += samplesRead;
				maxSamples -= samplesRead;
				samplesLeftInBlock -= samplesRead;
				samplePosition += len;

				UpdateBlockIfNeeded();
			}

			return bufferedSamples;
		}

		void UpdateBlockIfNeeded()
		{
			if (samplesLeftInBlock == 0)
			{
				while (currentBlock.MoveNext())
				{
					if (currentBlock.Current.Code != 1 && currentBlock.Current.Code != 9)
						continue;
					stream.Seek(currentBlock.Current.SampleBlock.Offset, SeekOrigin.Begin);
					samplesLeftInBlock = currentBlock.Current.SampleBlock.Samples;
					return;
				}

				currentBlockEnded = true;
			}
		}

		int Read(Span<byte> buffer)
		{
			var bytesWritten = 0;
			while (buffer.Length > 0)
			{
				var len = FillBuffer(buffer.Length);
				if (len == 0)
					break;
				this.buffer.AsSpan(..len).CopyTo(buffer);
				buffer = buffer[len..];
				bytesWritten += len;
			}

			return bytesWritten;
		}

		public class VocStream : Stream
		{
			readonly VocFormat format;
			public VocStream(VocFormat format)
			{
				this.format = format;
			}

			public override bool CanRead => format.samplePosition < format.totalSamples;
			public override bool CanSeek => false;
			public override bool CanWrite => false;

			public override long Length => format.totalSamples;

			public override long Position
			{
				get => format.samplePosition;
				set => throw new NotImplementedException();
			}

			public override int Read(byte[] buffer, int offset, int count)
			{
				return Read(buffer.AsSpan(offset, count));
			}

			public override int Read(Span<byte> buffer)
			{
				return format.Read(buffer);
			}

			public override void Flush() { throw new NotImplementedException(); }
			public override long Seek(long offset, SeekOrigin origin) { throw new NotImplementedException(); }
			public override void SetLength(long value) { throw new NotImplementedException(); }
			public override void Write(byte[] buffer, int offset, int count) { throw new NotImplementedException(); }
			public override void Write(ReadOnlySpan<byte> buffer) { throw new NotImplementedException(); }

			protected override void Dispose(bool disposing)
			{
				if (disposing)
					format.Dispose();
				base.Dispose(disposing);
			}
		}
	}
}
