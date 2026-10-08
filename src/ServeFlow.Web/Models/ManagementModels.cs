using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using ServeFlow.Application.Management;
using ServeFlow.Domain.Enums;

namespace ServeFlow.Web.Models;

public sealed class RestaurantSettingsModel
{
    [Required, StringLength(160)] public string Name { get; set; } = "";
    [Required, StringLength(100), Display(Name = "Time zone")] public string TimeZoneId { get; set; } = "";
    [Required, StringLength(12)] public string Version { get; set; } = "";
    [BindNever] public string CurrencyCode { get; set; } = "";
}

public sealed class CategoryModel
{
    public Guid Id { get; set; }
    [Required, StringLength(100)] public string Name { get; set; } = "";
    [Range(0, 10000), Display(Name = "Display order")] public int SortOrder { get; set; }
    [Display(Name = "Archive category")] public bool IsArchived { get; set; }
    [StringLength(12)] public string? Version { get; set; }
}

public sealed class MenuModel
{
    public Guid Id { get; set; }
    [Required, Display(Name = "Category")] public Guid CategoryId { get; set; }
    [Required, StringLength(160)] public string Name { get; set; } = "";
    [StringLength(2000)] public string? Description { get; set; }
    [Range(typeof(decimal), "0", "1000000")] public decimal Price { get; set; }
    [Display(Name = "Available to order")] public bool IsAvailable { get; set; } = true;
    [Display(Name = "Archive product")] public bool IsArchived { get; set; }
    [StringLength(12)] public string? Version { get; set; }
    [BindNever] public IReadOnlyList<CategoryChoice> CategoryChoices { get; set; } = [];
    [BindNever] public bool HasImage { get; set; }
}

public sealed class TableModel
{
    public Guid Id { get; set; }
    [Range(1, 10000), Display(Name = "Table number")] public int Number { get; set; } = 1;
    [Range(1, 100)] public int Capacity { get; set; } = 4;
    [EnumDataType(typeof(TableStatus))] public TableStatus Status { get; set; }
    [Display(Name = "Archive table")] public bool IsArchived { get; set; }
    [StringLength(12)] public string? Version { get; set; }
}

public sealed class TableStatusModel
{
    public Guid Id { get; set; }
    [EnumDataType(typeof(TableStatus))] public TableStatus Status { get; set; }
    [Required, StringLength(12)] public string Version { get; set; } = "";
    [BindNever] public int Number { get; set; }
}

public sealed class ImageModel
{
    [Required, StringLength(12)] public string Version { get; set; } = "";
    public IFormFile? Image { get; set; }
}
public sealed record PagingModel(int Page, int TotalPages, string Search, bool IncludeArchived);
