using System.Runtime.InteropServices;

namespace Nectar.Interop;

/// <summary>
/// User-scope DPAPI helpers (crypt32). The calendar refresh token is the only secret Nectar
/// stores, and it is encrypted with the signed-in user's Windows credentials so another account
/// on the same machine cannot read it. Every failure returns null instead of throwing: the
/// caller decides whether to skip persistence or report the problem.
/// </summary>
internal static class DataProtection
{
    private const int CryptProtectUiForbidden = 0x1;

    [StructLayout(LayoutKind.Sequential)]
    private struct DataBlob
    {
        internal int Size;
        internal IntPtr Data;
    }

    [DllImport("crypt32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CryptProtectData(
        ref DataBlob dataIn,
        string? description,
        IntPtr optionalEntropy,
        IntPtr reserved,
        IntPtr prompt,
        int flags,
        out DataBlob dataOut);

    [DllImport("crypt32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CryptUnprotectData(
        ref DataBlob dataIn,
        out IntPtr description,
        IntPtr optionalEntropy,
        IntPtr reserved,
        IntPtr prompt,
        int flags,
        out DataBlob dataOut);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr LocalFree(IntPtr handle);

    internal static byte[]? Protect(byte[] plain) => Protect(plain, "Nectar calendar credential");

    internal static byte[]? Protect(byte[] plain, string description)
    {
        if (plain.Length == 0)
        {
            return null;
        }

        var input = new DataBlob
        {
            Size = plain.Length,
            Data = Marshal.AllocHGlobal(plain.Length),
        };

        try
        {
            Marshal.Copy(plain, 0, input.Data, plain.Length);

            if (!CryptProtectData(
                    ref input,
                    description,
                    IntPtr.Zero,
                    IntPtr.Zero,
                    IntPtr.Zero,
                    CryptProtectUiForbidden,
                    out var output))
            {
                return null;
            }

            try
            {
                return Copy(output);
            }
            finally
            {
                LocalFree(output.Data);
            }
        }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException)
        {
            return null;
        }
        finally
        {
            Marshal.FreeHGlobal(input.Data);
        }
    }

    internal static byte[]? Unprotect(byte[] protectedBytes)
    {
        if (protectedBytes.Length == 0)
        {
            return null;
        }

        var input = new DataBlob
        {
            Size = protectedBytes.Length,
            Data = Marshal.AllocHGlobal(protectedBytes.Length),
        };

        try
        {
            Marshal.Copy(protectedBytes, 0, input.Data, protectedBytes.Length);

            if (!CryptUnprotectData(
                    ref input,
                    out var description,
                    IntPtr.Zero,
                    IntPtr.Zero,
                    IntPtr.Zero,
                    CryptProtectUiForbidden,
                    out var output))
            {
                return null;
            }

            try
            {
                return Copy(output);
            }
            finally
            {
                LocalFree(output.Data);
                if (description != IntPtr.Zero)
                {
                    LocalFree(description);
                }
            }
        }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException)
        {
            return null;
        }
        finally
        {
            Marshal.FreeHGlobal(input.Data);
        }
    }

    private static byte[] Copy(DataBlob blob)
    {
        var bytes = new byte[blob.Size];
        if (blob.Size > 0)
        {
            Marshal.Copy(blob.Data, bytes, 0, blob.Size);
        }

        return bytes;
    }
}
