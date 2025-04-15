# Log Processor

A multi-threaded log file processor with filtering and sorting capabilities.

## Features

- Processes multiple log files in parallel using all available CPU cores
- Supports filtering by keyword (case insensitive)
- Excludes lines containing specified substrings
- Cleans log lines from control characters
- Sorts results by date and time
- Handles Windows-1251 encoded files
- Provides detailed processing statistics

## Requirements

- .NET 6.0 or later
- Windows OS (due to encoding support)

## Configuration

Create `config.ini` file in the following format:

```ini
; Label=Path to log file
Server1=C:\logs\server1.log
Server2=C:\logs\server2.log

; Exclude lines containing these substrings (semicolon separated)
exclude=debug;test;temp