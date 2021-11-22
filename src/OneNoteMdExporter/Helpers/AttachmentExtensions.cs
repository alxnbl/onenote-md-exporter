//using alxnbl.OneNoteMdExporter.Models;
//using System;
//using System.IO;

//namespace alxnbl.OneNoteMdExporter.Helpers
//{
//    public static class AttachmentExtensions
//    {
//        public static void EnsureAttachmenentFileIsUnique(this Attachement attach, Func<Attachement, string> getAttachmentFilePathMethod)
//        {
//            var noUseFileNameFound = false;
//            var cmpt = 0;
//            var attachmentFilePath = getAttachmentFilePathMethod(attach);

//            while (!noUseFileNameFound)
//            {
//                var candidateFilePath = cmpt == 0 ? attachmentFilePath :
//                    $"{Path.ChangeExtension(attachmentFilePath, null)}-{cmpt}{Path.GetExtension(attachmentFilePath)}";

//                if (!File.Exists(candidateFilePath))
//                {
//                    if (cmpt > 0)
//                        attach.OverrideExportFilePath = candidateFilePath;

//                    noUseFileNameFound = true;
//                }
//                else
//                    cmpt++;
//            }
//        }
//    }
//}
