using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

public static class NativeArrayExtensions
{
	public static unsafe void CopyData<T>(this NativeArray<Triangle> src, T[] dst, int count) where T : unmanaged
	{
		void* srcPtr = src.GetUnsafePtr();
		fixed (T* dstPtr = dst)
		{
			UnsafeUtility.MemCpy(dstPtr, srcPtr, sizeof(T) * count);
		}
	}
}