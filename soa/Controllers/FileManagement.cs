using System.IO;

using Teamcenter.ClientX;
using Teamcenter.Services.Loose.Core._2006_03.FileManagement;
using Teamcenter.Services.Strong.Core;
using Teamcenter.Services.Strong.Core._2006_03.DataManagement;
using Teamcenter.Services.Strong.Core._2008_06.DataManagement;
using Teamcenter.Soa.Client;
using Teamcenter.Soa.Client.Model;
using Teamcenter.Soa.Exceptions;

namespace Teamcenter.FMS
{
    /**
     * Use the FileManagementService to transfer files
     */
    public class FileManagement
    {
        /** The number of datasets to upload in the multiple file example. */
        static int NUMBER_OF_DATASETS = 120;

        /** The number of files per dataset to upload in the multiple file example. */
        static int NUMBER_OF_FILES_PER_DATASET = 3;

        /** Upload some files using the FileManagement utilities. */
        public void uploadFiles()
        {
            FileManagementUtility fmsFileManagement = new FileManagementUtility(Session.getConnection());
            DataManagementService dmService = DataManagementService.getService(Session.getConnection());
            try
            {
                uploadSingleFile(fmsFileManagement, dmService);
                uploadMultipleFiles(fmsFileManagement, dmService);
            }
            finally
            {
                // Close FMS connection when done
                fmsFileManagement.Term();
            }
        }

        /** Uploads a single file using the FileManagement utilities. */
        public void uploadSingleFile(FileManagementUtility fmsFileManagement, DataManagementService dmService)
        {
            GetDatasetWriteTicketsInputData[] inputs = { getSingleGetDatasetWriteTicketsInputData(dmService) };
            ServiceData response = fmsFileManagement.PutFiles(inputs);

            if (response.sizeOfPartialErrors() > 0)
                System.Console.Out.WriteLine("FileManagementService single upload returned partial errrors: " + response.sizeOfPartialErrors());

            // Delete all objects created
            ModelObject[] datasets = { inputs[0].Dataset };
            dmService.DeleteObjects(datasets);
        }

        /** Uploads multiple files using the FileManagement utilities. */
        public void uploadMultipleFiles(FileManagementUtility fMSFileManagement, DataManagementService dmService)
        {
            GetDatasetWriteTicketsInputData[] inputs = getMultipleGetDatasetWriteTicketsInputData(dmService);
            ServiceData response = fMSFileManagement.PutFiles(inputs);

            if (response.sizeOfPartialErrors() > 0)
                System.Console.Out.WriteLine("FileManagementService multiple upload returned partial errrors: " + response.sizeOfPartialErrors());

            // Delete all objects created
            ModelObject[] datasets = new ModelObject[inputs.Length];
            for (int i = 0; i < inputs.Length; ++i)
            {
                datasets[i] = inputs[i].Dataset;
            }
            dmService.DeleteObjects(datasets);
        }

        /** @return A single GetDatasetWriteTicketsInputData for uploading ReadMe.txt. */
        private GetDatasetWriteTicketsInputData getSingleGetDatasetWriteTicketsInputData(DataManagementService dmService)
        {
            // Create a Dataset
            DatasetProperties2 props = new DatasetProperties2();
            props.ClientId = "datasetWriteTixTestClientId";
            props.Type = "Text";
            props.Name = "Sample-FMS-Upload";
            props.Description = "Testing put File";
            DatasetProperties2[] currProps = { props };

            CreateDatasetsResponse resp = dmService.CreateDatasets2(currProps);

            // Assume this file is in current dir
            FileInfo file1 = new FileInfo("ReadMe.txt");

            // Create a file to associate with dataset
            DatasetFileInfo fileInfo = new DatasetFileInfo();
            fileInfo.ClientId = "file_1";
            fileInfo.FileName = file1.FullName;
            fileInfo.NamedReferencedName = "Text";
            fileInfo.IsText = true;
            fileInfo.AllowReplace = false;
            DatasetFileInfo[] fileInfos = { fileInfo };

            GetDatasetWriteTicketsInputData inputData = new GetDatasetWriteTicketsInputData();
            inputData.Dataset = resp.Output[0].Dataset;
            inputData.CreateNewVersion = false;
            inputData.DatasetFileInfos = fileInfos;

            return inputData;
        }

        /**
         * @return An array of NUMBER_OF_DATASETS GetDatasetWriteTicketsInputData objects
         * for uploading NUMBER_OF_FILES_PER_DATASET copies of ReadMe.txt to each Dataset.
         */
        private GetDatasetWriteTicketsInputData[] getMultipleGetDatasetWriteTicketsInputData(DataManagementService dmService)
        {
            GetDatasetWriteTicketsInputData[] inputs = new GetDatasetWriteTicketsInputData[NUMBER_OF_DATASETS];
            DatasetProperties2[] currProps = new DatasetProperties2[inputs.Length];

            // Create a bunch of Datasets
            for (int i = 0; i < inputs.Length; ++i)
            {
                DatasetProperties2 props = new DatasetProperties2();
                props.ClientId = "datasetWriteTixTestClientId " + i;
                props.Type = "Text";
                props.Name = "Sample-FMS-Upload-" + i;
                props.Description = "Testing Multiple put File";
                currProps[i] = props;
            }

            CreateDatasetsResponse resp = dmService.CreateDatasets2(currProps);

            // Create files to associate with each Dataset
            for (int i = 0; i < inputs.Length; ++i)
            {
                DatasetFileInfo[] fileInfos = new DatasetFileInfo[NUMBER_OF_FILES_PER_DATASET];
                for (int j = 0; j < fileInfos.Length; ++j)
                {
                    DatasetFileInfo fileInfo = new DatasetFileInfo();

                    // Create different filenames to be uploaded into the same dataset
                    // Create or use this file in current dir
                    FileInfo file1 = new FileInfo("ReadMeCopy" + j + ".txt");
                    assureFileCreated(file1);

                    fileInfo.ClientId            = "Dataset " + i + " File " + j;
                    fileInfo.FileName            = file1.FullName;
                    fileInfo.NamedReferencedName = "Text";
                    fileInfo.IsText              = true;
                    fileInfo.AllowReplace        = false;
                    fileInfos[j] = fileInfo;
                }

                GetDatasetWriteTicketsInputData inputData = new GetDatasetWriteTicketsInputData();
                inputData.Dataset = resp.Output[i].Dataset;
                inputData.CreateNewVersion = false;
                inputData.DatasetFileInfos = fileInfos;

                inputs[i] = inputData;
            }
            return inputs;
        }

        /**
         * Assures that the file exists on the file system.
         * If not, this method copies "ReadMe.txt" to create the file.
         * @param file1 (FileInfo) The file to be created if it does not already exist.
         */
        private void assureFileCreated(FileInfo file1)
        {
            if (file1.Exists)
            {
                return;
            }

            try
            {
                // Assume this file is in current dir
                // and that we can copy it in the current dir
                File.Copy("ReadMe.txt", file1.Name);
            }
            catch(IOException ex)
            {
                System.Console.Out.WriteLine("Could not copy 'ReadMe.txt' to " + file1.Name
                    + "-" + ex.Message );
            }
        }
    }
}
