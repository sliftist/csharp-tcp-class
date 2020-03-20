using System;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using System.Threading;
using System.Text;
using System.Threading.Tasks;
using System.Linq.Expressions;
using System.IO;

namespace Sync
{
    public interface IMagic
    {
        string Test(System.Int32 arg, long test, string str);

        List<int> TakeList(List<int> test);
        ObjectObj TakeObj(ObjectObj obj);
    }
    public class ObjectObj { public int x; }

    public class Magic : IMagic
    {
        
        public List<int> TakeList(List<int> test)
        {
            return test;
        }
        public ObjectObj TakeObj(ObjectObj obj)
        {
            return obj;
        }

        public string Test(int arg, long test, string str){ return (arg + ", " + test + " " + str); }
    }

    public class Holder : IModelHolder
    {
        public object MethodCall(string methodName, object[] parameters)
        {
            Console.WriteLine(methodName + " called with " + string.Join(", ", parameters));
            return null;
        }
    }


    public interface IFileSync { }
    public class FileSync
    {
        IFileSync remoteClientSync;
        public FileSync(Stream stream)
        {
            remoteClientSync = DynamicWrapper.CreateClientModel<IFileSync>(new CallWriter(stream, typeof(IFileSync)));
        }

        public void SyncFiles(string sourceDir, string destDir, string[] fileFilters)
        {
            //Watch
            //Bulk sync

            foreach (string file in FileHelpers.EnumerateRoot(sourceDir, fileFilters))
            {

            }
        }

        public void GetHashes(string directory, int hashSize)
        {

        }
    }

    public static class FileHelpers
    {
        public class ReadResult
        {
            public string Dir;
            public string File;
        }

        public static IEnumerable<string> EnumerateRoot(string dir, string[] fileFilters)
        {
            Queue<IEnumerator<ReadResult>> reads = new Queue<IEnumerator<ReadResult>>();

            reads.Enqueue(EnumerateRead(dir, fileFilters).GetEnumerator());

            DateTime start = DateTime.Now;
            DateTime lastFile = DateTime.Now;

            while (reads.Count > 0)
            {
                var read = reads.Dequeue();
                if (!read.MoveNext()) continue;
                var current = read.Current;

                if (current.Dir != null)
                {
                    reads.Enqueue(EnumerateRead(current.Dir, fileFilters).GetEnumerator());
                }

                reads.Enqueue(read);

                if (current.File != null)
                {
                    lastFile = DateTime.Now;
                    yield return current.File;
                }
            }

            Console.WriteLine("All results in " + (lastFile - start).TotalMilliseconds + "ms");
        }
        public static IEnumerable<ReadResult> EnumerateRead(string dir, string[] fileFilters)
        {
            IEnumerable<string> files;
            try
            {
                files = fileFilters.Select(filter => Directory.EnumerateFiles(dir, filter)).SelectMany(x => x);
            }
            catch (PathTooLongException e) { yield break; }
            catch (UnauthorizedAccessException e) { yield break; }

            foreach (string file in files)
            {
                yield return new ReadResult() { File = file };
            }


            IEnumerable<string> dirs = null;
            try
            {
                dirs = Directory.EnumerateDirectories(dir, "*");
            }
            catch (PathTooLongException e) { yield break; }
            catch (UnauthorizedAccessException e) { yield break; }

            foreach (string subDir in dirs)
            {
                yield return new ReadResult() { Dir = subDir };
            }
        }

        public static void EnumerateDirect(string dir, List<string> outFiles, string[] fileFilters)
        {
            IEnumerable<string> files;
            try
            {
                files = fileFilters.Select(filter => Directory.EnumerateFiles(dir, filter)).SelectMany(x => x);
            }
            catch (PathTooLongException e) { return; }
            catch (UnauthorizedAccessException e) { return; }
            foreach (string file in files)
            {
                outFiles.Add(file);
            }

            IEnumerable<string> dirs = null;

            try
            {
                dirs = Directory.EnumerateDirectories(dir, "*");
            }
            catch (PathTooLongException e) { return; }
            catch (UnauthorizedAccessException e) { return; }
            foreach (string subDir in dirs)
            {
                EnumerateDirect(subDir, outFiles, fileFilters);
            }
        }
    }
}
