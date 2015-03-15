using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace PeerCastStation.Core
{
  public class DynamicRingBuffer<T>
  {
    T[] _buffer;
    long _bufsize;
    long _head, _tail;

    public DynamicRingBuffer(long minBufsize = 512)
    {
      _bufsize = 2;
      _bufsize = CalculateBufferSize(minBufsize);
      _buffer = new T[_bufsize];
      _head = _tail = 0;
    }

    public long Capacity
    {
      get
      {
        return _bufsize - 1;
      }
    }

    public void IncreaseCapacity(long requiredCapacity)
    {
      var size = CalculateBufferSize(requiredCapacity);
      var buffer = new T[size];

      CopyTo(buffer, 0);
      SwapBuffers(buffer);
    }

    public void PushBack(T data)
    {
      MakeSureNotFull();
      _tail = Wrap(_tail + 1);
      _buffer[_tail] = data;
    }

    public void PushFront(T data)
    {
      MakeSureNotFull();
      _head = Wrap(_head - 1);
      _buffer[_head] = data;
    }

    void MakeSureNotFull()
    {
      PrepareToAccommodate(1);
    }

    public void PrepareToAccommodate(long increment)
    {
      if (Capacity - Count < increment) {
        GrowToAccommodate(increment);
      }
    }

    public void PushBack(T[] data)
    {
      PrepareToAccommodate(data.Length);
      foreach (T datum in data) {
        PushBack(datum);
      }
    }

    public void PushFront(T[] data)
    {
      PrepareToAccommodate(data.Length);
      foreach (T datum in Enumerable.Reverse(data)) {
        PushFront(datum);
      }
    }

    public long ReadFront(T[] outbuf, long offset, long count)
    {
      var numToRead = Math.Min(Count, count);

      for (long i = 0; i < numToRead; i++) {
        outbuf[offset + i] = this[i];
      }
      return numToRead;
    }

    public long Read(T[] outbuf, long offset, long start, long count)
    {
      if (!IsIndexValid(start))
        return 0;

      var numToRead = Math.Min(Count - start, count);

      for (long i = 0; i < numToRead; i++) {
        outbuf[offset + i] = this[start + i];
      }
      return numToRead;
    }

    public long Write(T[] data, long offset, long start, long count)
    {
      if (start + count > Count) {
        var increment = start + count - Count;
        PrepareToAccommodate(increment);
        _tail = Wrap(_tail + increment);
      }

      for (long i = 0; i < count; i++) {
        this[start + i] = data[offset + i];
      }
      return count;
    }

    public long ReadBack(T[] outbuf, long offset, long count)
    {
      var numToRead = Math.Min(Count, count);

      for (long i = Count - numToRead; i < numToRead; i++) {
        outbuf[offset + i] = this[i];
      }
      return numToRead;
    }

    public void PopBack(T[] data, long offset, long count)
    {
      var bytesRead = ReadBack(data, offset, count);
      FreeBack(bytesRead);
    }

    public void FreeFront(long length)
    {
      if (length < 0 || length > Count)
        throw new InvalidOperationException("out of range");

      _head = Wrap(_head + length);
    }

    public void FreeBack(long length)
    {
      if (length < 0 || length > Count)
        throw new InvalidOperationException("out of range");

      _tail = Wrap(_tail - length);
    }

    public void PopFront(T[] data, long offset, long count)
    {
      var bytesRead = ReadFront(data, offset, count);
      FreeFront(bytesRead);
    }

    long CalculateBufferSize(long requirement)
    {
      var new_size = _bufsize;
      while (new_size <= requirement)
        new_size *= 2;
      return new_size;
    }

    void GrowToAccommodate(long increment)
    {
      IncreaseCapacity(Count + increment);
    }

    void SwapBuffers(T[] new_buffer)
    {
      var count = Count;

      _buffer = new_buffer;
      _head = 0;
      _tail = count;
      _bufsize = new_buffer.Length;
    }

    long Wrap(long index)
    {
      return index & (_bufsize - 1);
    }

    public T PopBack()
    {
      MakeSureNotEmpty();
      var ret = _buffer[_tail];
      _tail = Wrap(_tail - 1);
      return ret;
    }

    void MakeSureNotEmpty()
    {
      if (Count == 0) {
        throw new InvalidOperationException("buffer is empty");
      }
    }

    public T PopFront()
    {
      MakeSureNotEmpty();
      var ret = _buffer[_head];
      _head = Wrap(_head + 1);
      return ret;
    }

    public T[] ToArray()
    {
      T[] ary = new T[Count];

      CopyTo(ary, 0);

      return ary;
    }

    public void CopyTo(T[] ary, long start)
    {
      if (_head <= _tail) {
        Array.Copy(_buffer, _head, ary, start, Count);
      }
      else {
        Array.Copy(_buffer, _head, ary, start, _bufsize - _head);
        Array.Copy(_buffer, 0, ary, start + (_bufsize - _head), _tail);
      }
    }

    bool IsIndexValid(long index)
    {
      return index >= 0 && index < Count;
    }

    void ValidateIndex(long index)
    {
      if (!IsIndexValid(index))
        throw new IndexOutOfRangeException(index + " is out of range");
    }

    public T this[long index]
    {
      get
      {
        ValidateIndex(index);
        return _buffer[Wrap(_head + index)];
      }

      set
      {
        ValidateIndex(index);
        _buffer[Wrap(_head + index)] = value;
      }
    }

    public long Count
    {
      get
      {
        if (_head <= _tail) {
          return _tail - _head;
        }
        else {
          return _bufsize - _head + _tail;
        }
      }
    }
  }

  public class DynamicRingBufferStream : Stream
  {
    DynamicRingBuffer<byte> _buffer = new DynamicRingBuffer<byte>();

    public override bool CanRead
    {
      get { return true; }
    }

    public override bool CanSeek
    {
      get { return true; }
    }
    public override bool CanWrite
    {
      get { return true; }
    }

    public override long Length
    {
      get { return _buffer.Count; }
    }

    // ストリームの範囲を超えることもある
    public override long Position
    {
      get;
      set;
    }

    public DynamicRingBuffer<byte> GetBuffer()
    {
      return _buffer;
    }

    public override void Flush() { }

    public override int Read(byte[] buffer, int offset, int count)
    {
      var bytesRead = (int)_buffer.Read(buffer, offset, Position, count);
      Position += bytesRead;
      return bytesRead;
    }

    class LogicErrorException : Exception
    {
      public LogicErrorException(string message)
        : base(message)
      { }
    }

    public override long Seek(long offset, SeekOrigin origin)
    {
      switch (origin) {
      case SeekOrigin.Begin:
        if (offset < 0)
          throw new IOException("ストリームの先頭より前に位置を移動しようとしました。");
        return Position = offset;
      case SeekOrigin.Current:
        return Position += offset;
      case SeekOrigin.End:
        return Position = _buffer.Count + offset;
      default:
        throw new LogicErrorException("case not covered");
      }
    }

    public override void SetLength(long value)
    {
      if (value == _buffer.Count) {
        return;
      }
      else if (value > _buffer.Count) {
        var padding = new byte[value - _buffer.Count];
        _buffer.PushBack(padding);
      }
      else { // value < _buffer.Count
        // Positionが範囲を出ても調整はしない
        _buffer.FreeBack(_buffer.Count - value);
      }
    }

    public override void Write(byte[] buffer, int offset, int count)
    {
      _buffer.Write(buffer, offset, Position, count);
      Position += count;
    }

    // データを左にずらして先頭 length バイトを捨てる。
    // TODO: IOException うんちゃら
    public void Shift(long length)
    {
      _buffer.FreeFront(length);
      Position -= length;
    }
  }

}
