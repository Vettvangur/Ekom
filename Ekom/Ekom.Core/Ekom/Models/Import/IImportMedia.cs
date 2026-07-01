namespace Ekom.Models.Import;
public interface IImportMedia
{
    int? SortOrder { get; set; }
    ImportMediaAction Action { get; set; }
}

