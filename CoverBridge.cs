using System;
using System.Threading;
using Windows.Storage.Streams;
public static class CoverBridge {
 public static byte[] Read(object value) {
  var stream=(IRandomAccessStream)value;
  if(stream.Size>10000000) return null;
  using(var reader=new DataReader(stream.GetInputStreamAt(0))) {
  var op=reader.LoadAsync((uint)stream.Size);
  for(int i=0;i<300 && op.Status==Windows.Foundation.AsyncStatus.Started;i++) Thread.Sleep(10);
  if(op.Status!=Windows.Foundation.AsyncStatus.Completed) {op.Cancel();return null;}
  var data=new byte[op.GetResults()]; reader.ReadBytes(data); return data;
  }
 }
}
