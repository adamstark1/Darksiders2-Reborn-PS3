using System;
using System.IO;
using System.Text;

namespace Darksiders2Reborn.Launcher.Services;

public enum EbootPatchStatus
{
    Patched,
    AlreadyPatched,
    EncryptedSelf,
    NotElf,
    UnknownVersion,
    Error
}

public sealed record EbootPatchResult(EbootPatchStatus Status, string Message, string? OutputPath = null);

public static class EbootPatcherService
{
    private static readonly byte[] OriginalPem = Encoding.ASCII.GetBytes(string.Join("\n", new[]
    {
        "-----BEGIN CERTIFICATE-----",
        "MIICVTCCAb6gAwIBAgIET4cxljANBgkqhkiG9w0BAQUFADBvMQswCQYDVQQGEwJD",
        "QTELMAkGA1UECBMCQkMxEjAQBgNVBAcTCVZhbmNvdXZlcjERMA8GA1UEChMIVEhR",
        "IEluYy4xETAPBgNVBAsTCFRIUSBJbmMuMRkwFwYDVQQDDBAqLnRocWNvbm5lY3Qu",
        "Y29tMB4XDTEyMDQxMjE5NDgzOFoXDTIyMDQxMDE5NDgzOFowbzELMAkGA1UEBhMC",
        "Q0ExCzAJBgNVBAgTAkJDMRIwEAYDVQQHEwlWYW5jb3V2ZXIxETAPBgNVBAoTCFRI",
        "USBJbmMuMREwDwYDVQQLEwhUSFEgSW5jLjEZMBcGA1UEAwwQKi50aHFjb25uZWN0",
        "LmNvbTCBnzANBgkqhkiG9w0BAQEFAAOBjQAwgYkCgYEArUF1zVw7tTfsNOuA57sP",
        "ofrRZmSNUweNBTvhjG6p1x1wJ71WZGpPpqHdHnsSJxo2Z6rYNT5AByqieFxZUmV/",
        "x9jxrZ6tGC+vNv+vDtvRTE5bPN8PyUO1/AR8AfolUBjbAzbD1ZXA3NiOYR1bKpOc",
        "/um/nee2cNPHc/OWQgU3PeMCAwEAATANBgkqhkiG9w0BAQUFAAOBgQAluvRoIE9n",
        "UWBCNFkoQLRUN2zIHyf/VJPNZX5BlMkP0zjmwm323jI8GOkJcPnlMOd7IJlS74ZD",
        "DjtDhSEVNhQ5TT34AE38SwmDY+YC1gG7vMeegOqjSrEvJcSLA0+1WOjgo33I2v8W",
        "xOHEBtbZhUVQMyDROcwwKZh0vTyakpK6Uw==",
        "-----END CERTIFICATE-----"
    }));

    private static readonly byte[] NewPem = Encoding.ASCII.GetBytes(string.Join("\n", new[]
    {
        "-----BEGIN CERTIFICATE-----",
        "MIICVTCCAb4CCQFaWlpaWlpaWjANBgkqhkiG9w0BAQUFADBvMQswCQYDVQQGEwJD",
        "QTELMAkGA1UECAwCQkMxEjAQBgNVBAcMCVZhbmNvdXZlcjERMA8GA1UECgwIVEhR",
        "IEluYy4xETAPBgNVBAsMCFRIUSBJbmMuMRkwFwYDVQQDDBAqLnRocWNvbm5lY3Qu",
        "Y29tMB4XDTIwMDEwMTAwMDAwMFoXDTM3MDEwMTAwMDAwMFowbzELMAkGA1UEBhMC",
        "Q0ExCzAJBgNVBAgMAkJDMRIwEAYDVQQHDAlWYW5jb3V2ZXIxETAPBgNVBAoMCFRI",
        "USBJbmMuMREwDwYDVQQLDAhUSFEgSW5jLjEZMBcGA1UEAwwQKi50aHFjb25uZWN0",
        "LmNvbTCBnzANBgkqhkiG9w0BAQEFAAOBjQAwgYkCgYEAxdLJmVw2HHVZzF1PK5TE",
        "Bif0hWs2+XX2VP2fkXWuRaxq4Yv4t6w48LHhtRIavQJe+CHavTMKynUYV9LSSHU3",
        "etDaDRdL5M87KOE42g9tmluUPidTZ5UMbIKR2gg9BeCJg/sIv9s3yGneWOXvk53w",
        "bIlXUdMA0+U2d1hzTDRJnZECAwEAATANBgkqhkiG9w0BAQUFAAOBgQAbO/9q54Zg",
        "QtfSslQEeRd8lqIK90zQEMUZl8cI+GsXxt3CmDREwX1beyHr139jvgG50cQdEuMO",
        "FAyfN0tH7arVBx7O1yrFmsBgDqqaRqCFIRYOt9yrv1DSbrtr9s1taC5kz3IHoWzm",
        "T5Q85U6CnEDAfCOhNYlRSDIZ8KiNgwOFiQ==",
        "-----END CERTIFICATE-----"
    }));

    static EbootPatcherService()
    {
        if (OriginalPem.Length != NewPem.Length)
            throw new InvalidOperationException("Certificate lengths differ.");
    }

    public static EbootPatchResult Patch(string inputPath, string? outputPath = null)
    {
        try
        {
            if (!File.Exists(inputPath))
                return new(EbootPatchStatus.Error, "File not found.");

            byte[] data = File.ReadAllBytes(inputPath);

            if (data.Length >= 4 && data[0] == 0x53 && data[1] == 0x43 && data[2] == 0x45 && data[3] == 0x00)
                return new(EbootPatchStatus.EncryptedSelf,
                    "This is an encrypted EBOOT.BIN (SELF). Decrypt it in RPCS3 first (Utilities > Decrypt PS3 Binaries) and select the resulting eboot.elf.");

            if (data.Length < 4 || data[0] != 0x7F || data[1] != 0x45 || data[2] != 0x4C || data[3] != 0x46)
                return new(EbootPatchStatus.NotElf, "The file is not an ELF, so it cannot be patched.");

            int replaced = ReplaceAll(data, OriginalPem, NewPem);
            if (replaced == 0)
            {
                return data.AsSpan().IndexOf(NewPem) >= 0
                    ? new(EbootPatchStatus.AlreadyPatched, "This file is already patched.")
                    : new(EbootPatchStatus.UnknownVersion,
                        "Original certificate not found: unsupported game version. Nothing was written.");
            }

            string output = outputPath ?? inputPath;
            File.WriteAllBytes(output, data);
            return new(EbootPatchStatus.Patched, $"Done: {replaced} certificate(s) replaced.", output);
        }
        catch (Exception ex)
        {
            return new(EbootPatchStatus.Error, "Error: " + ex.Message);
        }
    }

    private static int ReplaceAll(byte[] data, byte[] oldBytes, byte[] newBytes)
    {
        int count = 0;
        int pos = 0;
        while (pos <= data.Length - oldBytes.Length)
        {
            int idx = data.AsSpan(pos).IndexOf(oldBytes);
            if (idx < 0) break;
            int abs = pos + idx;
            newBytes.AsSpan().CopyTo(data.AsSpan(abs));
            count++;
            pos = abs + oldBytes.Length;
        }
        return count;
    }
}