using ICSharpCode.SharpZipLib.GZip;
using ICSharpCode.SharpZipLib.Tar;


void CreateTarGZ(string tgzFilename, string fileName)
{
    using (var outStream = File.Create(tgzFilename))
    using (var gzoStream = new GZipOutputStream(outStream))
    using (var tarArchive = TarArchive.CreateOutputTarArchive(gzoStream))
    {
        tarArchive.RootPath = Path.GetDirectoryName(fileName);

        var tarEntry = TarEntry.CreateEntryFromFile(fileName);
        tarEntry.Name = Path.GetFileName(fileName);

        tarArchive.WriteEntry(tarEntry, true);
    }
}