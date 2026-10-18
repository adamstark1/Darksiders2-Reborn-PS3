#!/bin/bash

if [ -z "$1" ]; then
    echo "[ERROR]: No EBOOT file specified."
    echo "Usage: ./patch_eboot.sh <path_to_eboot.elf>"
    exit 1
fi

FILE="$1"

if [ ! -f "$FILE" ]; then
    echo "[ERROR]: File not found."
    exit 1
fi

MAGIC=$(od -A n -N 4 -t x1 "$FILE" | tr -d ' \n')

if [ "$MAGIC" = "53434500" ]; then
    echo "[ERROR]: Encrypted SELF file. Decrypt with RPCS3 first."
    exit 1
fi

if [ "$MAGIC" != "7f454c46" ]; then
    echo "[ERROR]: Not a valid ELF file."
    exit 1
fi

export ORIG_PEM="-----BEGIN CERTIFICATE-----
MIICVTCCAb6gAwIBAgIET4cxljANBgkqhkiG9w0BAQUFADBvMQswCQYDVQQGEwJD
QTELMAkGA1UECBMCQkMxEjAQBgNVBAcTCVZhbmNvdXZlcjERMA8GA1UEChMIVEhR
IEluYy4xETAPBgNVBAsTCFRIUSBJbmMuMRkwFwYDVQQDDBAqLnRocWNvbm5lY3Qu
Y29tMB4XDTEyMDQxMjE5NDgzOFoXDTIyMDQxMDE5NDgzOFowbzELMAkGA1UEBhMC
Q0ExCzAJBgNVBAgTAkJDMRIwEAYDVQQHEwlWYW5jb3V2ZXIxETAPBgNVBAoTCFRI
USBJbmMuMREwDwYDVQQLEwhUSFEgSW5jLjEZMBcGA1UEAwwQKi50aHFjb25uZWN0
LmNvbTCBnzANBgkqhkiG9w0BAQEFAAOBjQAwgYkCgYEArUF1zVw7tTfsNOuA57sP
ofrRZmSNUweNBTvhjG6p1x1wJ71WZGpPpqHdHnsSJxo2Z6rYNT5AByqieFxZUmV/
x9jxrZ6tGC+vNv+vDtvRTE5bPN8PyUO1/AR8AfolUBjbAzbD1ZXA3NiOYR1bKpOc
/um/nee2cNPHc/OWQgU3PeMCAwEAATANBgkqhkiG9w0BAQUFAAOBgQAluvRoIE9n
UWBCNFkoQLRUN2zIHyf/VJPNZX5BlMkP0zjmwm323jI8GOkJcPnlMOd7IJlS74ZD
DjtDhSEVNhQ5TT34AE38SwmDY+YC1gG7vMeegOqjSrEvJcSLA0+1WOjgo33I2v8W
xOHEBtbZhUVQMyDROcwwKZh0vTyakpK6Uw==
-----END CERTIFICATE-----"

export NEW_PEM="-----BEGIN CERTIFICATE-----
MIICVTCCAb4CCQFaWlpaWlpaWjANBgkqhkiG9w0BAQUFADBvMQswCQYDVQQGEwJD
QTELMAkGA1UECAwCQkMxEjAQBgNVBAcMCVZhbmNvdXZlcjERMA8GA1UECgwIVEhR
IEluYy4xETAPBgNVBAsMCFRIUSBJbmMuMRkwFwYDVQQDDBAqLnRocWNvbm5lY3Qu
Y29tMB4XDTIwMDEwMTAwMDAwMFoXDTM3MDEwMTAwMDAwMFowbzELMAkGA1UEBhMC
Q0ExCzAJBgNVBAgMAkJDMRIwEAYDVQQHDAlWYW5jb3V2ZXIxETAPBgNVBAoMCFRI
USBJbmMuMREwDwYDVQQLDAhUSFEgSW5jLjEZMBcGA1UEAwwQKi50aHFjb25uZWN0
LmNvbTCBnzANBgkqhkiG9w0BAQEFAAOBjQAwgYkCgYEAxdLJmVw2HHVZzF1PK5TE
Bif0hWs2+XX2VP2fkXWuRaxq4Yv4t6w48LHhtRIavQJe+CHavTMKynUYV9LSSHU3
etDaDRdL5M87KOE42g9tmluUPidTZ5UMbIKR2gg9BeCJg/sIv9s3yGneWOXvk53w
bIlXUdMA0+U2d1hzTDRJnZECAwEAATANBgkqhkiG9w0BAQUFAAOBgQAbO/9q54Zg
QtfSslQEeRd8lqIK90zQEMUZl8cI+GsXxt3CmDREwX1beyHr139jvgG50cQdEuMO
FAyfN0tH7arVBx7O1yrFmsBgDqqaRqCFIRYOt9yrv1DSbrtr9s1taC5kz3IHoWzm
T5Q85U6CnEDAfCOhNYlRSDIZ8KiNgwOFiQ==
-----END CERTIFICATE-----"

DIR=$(dirname "$FILE")
FILENAME=$(basename "$FILE")
TARGET_FILE="$DIR/EBOOT.BIN"

HAS_NEW=$(perl -0777 -ne 'print "1" if index($_, $ENV{NEW_PEM}) != -1' "$FILE")
if [ "$HAS_NEW" = "1" ]; then
    if [ "$FILENAME" != "EBOOT.BIN" ] && [ "$FILENAME" != "eboot.bin" ]; then
        mv "$FILE" "$TARGET_FILE"
        echo "[SUCCESS]: Already patched. File renamed to EBOOT.BIN."
    else
        echo "[SUCCESS]: Already patched."
    fi
    exit 0
fi

HAS_ORIG=$(perl -0777 -ne 'print "1" if index($_, $ENV{ORIG_PEM}) != -1' "$FILE")
if [ "$HAS_ORIG" != "1" ]; then
    echo "[ERROR]: Original certificate not found. Unsupported version."
    exit 1
fi

perl -0777 -pi -e 's/\Q$ENV{ORIG_PEM}\E/$ENV{NEW_PEM}/g' "$FILE"

if [ "$FILENAME" != "EBOOT.BIN" ] && [ "$FILENAME" != "eboot.bin" ]; then
    mv "$FILE" "$TARGET_FILE"
    echo "[SUCCESS]: Successfully patched and renamed to EBOOT.BIN."
else
    echo "[SUCCESS]: Successfully patched ELF file."
fi