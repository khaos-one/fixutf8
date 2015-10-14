using System;
using System.Collections.Generic;
using System.IO;

namespace fixutf8 {
    class Program {
        static string InputFile;
        static string OutputFile; 
        static int ChunkSize;

        static string GetUsage() {
            return "fixutf8 1.0.0\n" +
                   "By Egor khaos Zelensky <i@khaos.su>, 2015\n\n" +
                   "USAGE: fixutf8 inputfile outputfile\n\n" +
                   "This program is designed to fix UTF-8 characters that was broken by 8-bit misencoding.";
        }

        static void Fail(string message = null, int exitCode = 1, bool dontExit = false) {
            if (!string.IsNullOrWhiteSpace(message)) {
                WriteLine($"ERROR: {message}", ConsoleColor.Red);
            } else {
                WriteLine(GetUsage());
            }

            if (!dontExit) {
                Environment.Exit(exitCode);
            }
        }

        static void WriteLine(string message, ConsoleColor? foregroundColor = null, ConsoleColor? backgroundColor = null) {
            Write(message + Environment.NewLine, foregroundColor, backgroundColor);
        }

        static void Write(string message, ConsoleColor? foregroundColor = null, ConsoleColor? backgroundColor = null) {
            var fcol = Console.ForegroundColor;
            var bcol = Console.BackgroundColor;

            if (foregroundColor != null) {
                Console.ForegroundColor = foregroundColor.Value;
            }
            if (backgroundColor != null) {
                Console.BackgroundColor = backgroundColor.Value;
            }

            Console.Write(message);

            if (foregroundColor != null) {
                Console.ForegroundColor = fcol;
            }
            if (backgroundColor != null) {
                Console.BackgroundColor = bcol;
            }
        }

        static bool InRange(byte b, byte lower, byte upper) {
            return lower <= b && b <= upper;
        }

        static bool IsUtf8CharSequence(byte[] arr, int pos, int count = -1) {
            if (count == -1) {
                count = arr.Length;
            }

            if (pos >= count)
                return false;
            
            if (InRange(arr[pos], 0x00, 0x7F) && arr[pos] != 0x3F)
                return true;

            if (InRange(arr[pos], 0xC0, 0xDF) && pos + 1 < count) {
                return InRange(arr[pos + 1], 0x80, 0xBF);
            }

            if (InRange(arr[pos], 0xE0, 0xEF) && pos + 2 < count) {
                return (InRange(arr[pos + 1], 0x80, 0xBF) && InRange(arr[pos + 2], 0x80, 0xBF));
            }

            if (InRange(arr[pos], 0xF0, 0xF7) && pos + 3 < count) {
                return (InRange(arr[pos + 1], 0x80, 0xBF) && InRange(arr[pos + 2], 0x80, 0xBF) && InRange(arr[pos + 3], 0x80, 0xBF));
            }

            return false;
        }

        static int FindBoundary(byte[] arr, int count = -1) {
            if (count == -1) {
                count = arr.Length;
            }

            for (var i = count - 1; i > 0; i--) {
                //if (arr[i] == 0x0A)
                //    return i;
                if (IsUtf8CharSequence(arr, i, count)) {
                    return i;
                }
            }

            return -1;
        }

        /// <summary>
        /// Do the fixing.
        /// </summary>
        /// <param name="arr"></param>
        /// <param name="offset"></param>
        /// <param name="count"></param>
        //static void FixUtf8Cp1251(ref byte[] arr, int offset = 0, int count = -1) {
        //    if (count == -1) {
        //        count = arr.Length;
        //    }

        //    for (var i = offset; i < count;) {
        //        if (0xC0 <= arr[i] && arr[i] <= 0xDF && i + 1 < count) {
        //            if (arr[i + 1] == 0x3F) {
        //                arr[i + 1] = 0x98;
        //                i += 2;
        //            }
        //        }
        //        else if (0xE0 <= arr[i] && arr[i] <= 0xEF && i + 2 < count) {
        //            if (arr[i + 1] == 0x3F) {
        //                arr[i + 1] = 0x98;

        //                if (arr[i + 2] == 0x3F) {
        //                    arr[i + 2] = 0x98;
        //                }
        //            }

        //            if (arr[i + 2] == 0x3F) {
        //                arr[i + 2] = 0x98;
        //            }

        //            i += 3;
        //        }
        //        else if (0xF0 <= arr[i] && arr[i] <= 0xF7 && i + 3 < count) {
        //            for (var j = i; j < i + 4; j++) {
        //                if (arr[j] == 0x3F) {
        //                    arr[j] = 0x98;
        //                }
        //            }

        //            i += 4;
        //        }
        //    }
        //}

        /// <summary>
        /// Do the fixing in naughty unsafe way.
        /// </summary>
        /// <param name="arr"></param>
        /// <param name="offset"></param>
        /// <param name="count"></param>
        private static unsafe uint FixUtf8Cp1251(byte* arr, int offset, int count) {
            unchecked {
                var replacements = 0U;

                for (var i = offset; i < count;) {
                    // 2-byte UTF8 char.
                    if (0xC0 <= arr[i] && arr[i] <= 0xDF && i + 1 < count) {
                        if (arr[i + 1] == 0x3F) {
                            arr[i + 1] = 0x98;
                            replacements++;
                        }

                        if (!(0x80 <= arr[i + 1] && arr[i + 1] <= 0xBF)) {
                            // It's broken UTF8 character. In common case we cannot know what it originally was.
                            // So we replace them with a double dot (0x2E).
                            for (var k = i; k < i + 1; k++) {
                                // Non-ASCII char.
                                if (arr[k] > 0x7F) {
                                    arr[k] = 0x2E;
                                }

                                replacements++;
                            }

                            //arr[i] = 0x2E;
                            //arr[i + 1] = 0x2E;
                            //replacements += 2;
                        }

                        i += 2;
                    }
                    // 3-byte UTF8 char.
                    else if (0xE0 <= arr[i] && arr[i] <= 0xEF && i + 2 < count) {
                        for (var j = i + 1; j < i + 3; j++) {
                            if (arr[j] == 0x3F) {
                                arr[j] = 0x98;
                                replacements++;
                            } else if (!(0x80 <= arr[j] && arr[j] <= 0xBF)) {
                                // It's broken UTF8 character. In common case we cannot know what it originally was.
                                // Do the lookup and replace all non-ascii chars in place of this UTF8 sequence.

                                for (var k = i; k < i + 3; k++) {
                                    // Non-ASCII char.
                                    if (arr[k] > 0x7F) {
                                        arr[k] = 0x2E;
                                    }

                                    replacements++;
                                }

                                //arr[i] = 0x2E;
                                //arr[i + 1] = 0x2E;
                                //arr[i + 2] = 0x2E;
                                //replacements += 3;
                                break;
                            }
                        }

                        i += 3;
                    }
                    // 4-byte UTF8 char.
                    else if (0xF0 <= arr[i] && arr[i] <= 0xF7 && i + 3 < count) {
                        for (var j = i + 1; j < i + 3; j++) {
                            if (arr[j] == 0x3F) {
                                arr[j] = 0x98;
                                replacements++;
                            } else if (!(0x80 <= arr[j] && arr[j] <= 0xBF)) {
                                // It's broken UTF8 character. In common case we cannot know what it originally was.
                                // Do the lookup and replace all non-ascii chars in place of this UTF8 sequence.

                                for (var k = i; k < i + 4; k++) {
                                    // Non-ASCII char.
                                    if (arr[k] > 0x7F) {
                                        arr[k] = 0x2E;
                                    }

                                    replacements++;
                                }

                                //arr[i] = 0x2E;
                                //arr[i + 1] = 0x2E;
                                //arr[i + 2] = 0x2E;
                                //replacements += 3;
                                break;
                            }
                        }

                        i += 4;
                    }
                    else if (arr[i] <= 0x7F) {
                        // Normal ASCII char.
                        i++;
                    }
                    else {
                        // Character obviously was one part of UTF8 multibyte code, but now the info is lost.
                        // Replace it with a dot (0x2E).
                        arr[i] = 0x2E;
                        replacements++;
                        i++;
                    }
                }

                return replacements;
            }
        }

        /// <summary>
        /// Main entry point.
        /// </summary>
        /// <param name="args"></param>
        unsafe static void Main(string[] args) {
            if (args.Length < 2) {
                Fail();
            }

            InputFile = Path.GetFullPath(args[0]);
            OutputFile = Path.GetFullPath(args[1]);
            ChunkSize = 5000000;

            // Check files existance.
            if (!File.Exists(InputFile)) {
                Fail($"Input file `{InputFile}` was not found.");
            }

            // Find out file parameters;
            var fi = new FileInfo(InputFile);
            var fileLength = fi.Length;

            // Do processing.
            var chunkDelta = 0;
            var i = 0L;
            var replacements = 0L;
            byte[] temp = null;

            WriteLine($"Input file: `{InputFile}`.");
            WriteLine($"Output file: `{OutputFile}`.");

            using (var input = File.OpenRead(InputFile)) {
                using (var output = File.OpenWrite(OutputFile)) {
                    while (i < fileLength) {
                        var buffer = new byte[ChunkSize];
                        var read = input.Read(buffer, chunkDelta, ChunkSize - chunkDelta);

                        if (chunkDelta > 0) {
                            Buffer.BlockCopy(temp, 0, buffer, 0, chunkDelta);
                            read += chunkDelta;
                            chunkDelta = 0;
                        }

                        if (read == -1) {
                            // We're done.
                            fixed (byte* p = temp) {
                                replacements += FixUtf8Cp1251(p, 0, temp.Length);
                            }

                            output.Write(temp, 0, temp.Length);
                            break;
                        }

                        var j = FindBoundary(buffer, read);

                        if (j == -1) {
                            continue;
                        }

                        chunkDelta = ChunkSize - j;

                        if (chunkDelta > 0) {
                            temp = new byte[chunkDelta];
                            Buffer.BlockCopy(buffer, j, temp, 0, chunkDelta);
                        }

                        fixed (byte* p = buffer) {
                            replacements += FixUtf8Cp1251(p, 0, j);
                        }

                        output.Write(buffer, 0, j);
                        Write($"\rProcessed {i:N0} of {fileLength:N0} bytes, replaced {replacements:N0} bytes.", ConsoleColor.Magenta);

                        i += read;
                        GC.Collect();
                    }
                }
            }

            WriteLine("");
            WriteLine("SUCCESS: All done.", ConsoleColor.Green);
        }
    }
}
