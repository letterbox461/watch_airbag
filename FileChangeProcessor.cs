using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Net.Mail;
using System.Configuration;

namespace Watch_airbag

{
    
    public class FileChangeProcessor
    {
        private FileSystemWatcher watcher;
        private BlockingCollection<string> queue;
        private ConcurrentDictionary<string, long> processedFileMap;
        private HttpClient? httpClient;
        private readonly String tempFile;
        private readonly String urlAddress;
        private readonly String watchFolder;
     

        public FileChangeProcessor()

        {
            watchFolder = @ConfigurationManager.AppSettings["WatchFolder"]!;    
            watcher = new FileSystemWatcher(watchFolder, filter: "*.csv");
            queue = new BlockingCollection<string>();
            processedFileMap = new ConcurrentDictionary<string, long>();
            httpClient = new();
            tempFile = @ConfigurationManager.AppSettings["TempFile"]!;
            urlAddress = @ConfigurationManager.AppSettings["UploadURL"]!;

            watcher.Changed += (_, e) => queue.Add(e.FullPath);
            watcher.Created += (_, e) => queue.Add(e.FullPath);
        }

        public async void StartProcessingFileChanges()
        {
            //Start watcher
            watcher.EnableRaisingEvents = true;

            //Start consuming queue
            while (!queue.IsCompleted)
            {
                
                var filePath = queue.Take(); //Blocking dequeue
                var fileInfo = new FileInfo(filePath);

                if (!fileInfo.Exists)
                    continue;

                if (processedFileMap.TryGetValue(filePath, out long processedWithModLength))
                {
                    Console.WriteLine(processedWithModLength);
                    Console.WriteLine(fileInfo.Length);
                    Console.WriteLine();
                    if (processedWithModLength == fileInfo.Length || fileInfo.Length==0)
                    {
                        Console.WriteLine($"Ignoring duplicate change event for file: {filePath}");                        
                        continue;
                    }

                    //It's a new change, so process it, then update mod date.
                    Console.WriteLine($"Processed file again: {filePath}");
                    processedFileMap[filePath] = fileInfo.Length;
                    File.Copy(filePath,tempFile, true);
                    await UploadFile(tempFile);
                    
                }
                else
                {
                    //We haven't processed this file before. Process it, then save the mod date.
                    Console.WriteLine($"Processed file for the first time: {filePath}.");
                    processedFileMap.TryAdd(filePath, fileInfo.Length);
                    File.Copy(filePath, tempFile, true);
                    await UploadFile(tempFile);                    
                }
            }     
            
        }
    
    async private Task<String> UploadFile(String filePath)
        {
            using (var multipartFormContent = new MultipartFormDataContent())
            {
                //Load the file and set the file's Content-Type header
                var fileStreamContent = new StreamContent(File.OpenRead(filePath));
                fileStreamContent.Headers.ContentType = new MediaTypeHeaderValue("text/csv");

                //Add the file
                multipartFormContent.Add(fileStreamContent, name: "file", fileName: "temp.csv");

                //Send it        
               return await Send(urlAddress, multipartFormContent);                                 
               
            }
        } 
    async private Task<String> Send(String url, MultipartFormDataContent content) {
            try

            {
                HttpResponseMessage response = await httpClient!.PostAsync(url, content);
                response.EnsureSuccessStatusCode();
                return await response.Content.ReadAsStringAsync();
            }
            
            catch(Exception ex)
            
            {
                Console.WriteLine("HTTP Failure, retrying", ex);
                Thread.Sleep(1000);
                return await Send(url, content);
            }          
        }       
    }
}
