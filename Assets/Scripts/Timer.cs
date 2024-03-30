using System;
using System.Diagnostics;

public class Timer : IDisposable
{
	private readonly Stopwatch stopwatch;
	private readonly string name;
	private readonly Action<long> callback;

	public Timer(Action<long> callback)
	{
		this.callback = callback;
		this.stopwatch = new Stopwatch();
		this.stopwatch.Start();
	}

	public void Dispose()
	{
		callback?.Invoke(stopwatch.ElapsedMilliseconds);
	}
}