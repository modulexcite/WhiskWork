﻿using System;
using Abb.One.MicroWebServer;
using System.IO;
using WhiskWork.AWS.SimpleDB;
using WhiskWork.Core;

namespace WhiskWork.CommondLineWebServer
{
    internal class Program
    {
        private static void Main(string[] args)
        {
            Console.WriteLine(Directory.GetCurrentDirectory());
            var port = 5555;
            string webRootDirectory = null;
            string logFilePath = null;
            string awsAccessKey = null;
            string awsSecretAccessKey = null;
            string domainPrefix = null;

            if(args.Length>0)
            {
                if(!int.TryParse(args[0], out port))
                {
                    Console.WriteLine("Not a valid port. Using default port 5555");
                    port = 5555;
                }

            }

            if(args.Length>1)
            {
                webRootDirectory = args[1];
                Console.WriteLine("Directory:{0}", webRootDirectory);
                if (!Directory.Exists(webRootDirectory))
                {
                    Console.WriteLine("Web directory does not exist. All file requests will return HTTP 403 Not found");
                    webRootDirectory = null;    
                }
            }

            if(args.Length>2)
            {
                logFilePath = args[2];
            }

            if(args.Length>5)
            {
                awsAccessKey = args[3];
                awsSecretAccessKey = args[4];
                domainPrefix = args[5];
            }

            IWorkStepRepository workStepRepository = new MemoryWorkStepRepository();
            IWorkItemRepository workItemRepository = new MemoryWorkItemRepository();

            if(awsAccessKey!=null && awsSecretAccessKey!=null)
            {
                workItemRepository = new CachingWorkItemRepository(new OptimisticAsynchWorkItemRepository(new SimpleDBWorkItemRepository(domainPrefix + "_items", awsAccessKey, awsSecretAccessKey)));
                workStepRepository = new CachingWorkStepRepository(new SimpleDBWorkStepRepository(domainPrefix+"_steps", awsAccessKey,awsSecretAccessKey));
            }

            var router = new WebRouter(new WorkflowRepository(workItemRepository, workStepRepository) , webRootDirectory, logFilePath);
            var server = new WebServer(router.ProcessRequest, port);

            Console.WriteLine("Started port:{0} directory:'{1}' logfile:'{2}'",port,webRootDirectory,logFilePath);
            server.Start();
        }
    }
}