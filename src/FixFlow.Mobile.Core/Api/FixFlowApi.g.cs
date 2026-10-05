
using Refit;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

#nullable enable annotations

namespace FixFlow.Mobile.Core.Api
{
    [System.CodeDom.Compiler.GeneratedCode("Refitter", "2.3.0.0")]
    public partial interface IFixFlowApi
    {
        [Headers("Accept: application/problem+json, application/json", "Content-Type: application/json")]
        [Post("/api/v1/auth/login")]
        Task<IApiResponse<AuthTokensResponse>> LoginAsync([Body] LoginRequest body, CancellationToken cancellationToken = default);

        [Headers("Accept: application/problem+json, application/json", "Content-Type: application/json")]
        [Post("/api/v1/auth/refresh")]
        Task<IApiResponse<AuthTokensResponse>> RefreshTokensAsync([Body] RefreshRequest body, CancellationToken cancellationToken = default);

        [Headers("Accept: application/problem+json", "Content-Type: application/json")]
        [Post("/api/v1/auth/logout")]
        Task<IApiResponse> LogoutAsync([Body] LogoutRequest body, CancellationToken cancellationToken = default);

        [Headers("Accept: application/json, application/problem+json")]
        [Get("/api/v1/users/me")]
        Task<IApiResponse<UserResponse>> GetCurrentUserAsync(CancellationToken cancellationToken = default);

        [Headers("Accept: application/json, application/problem+json")]
        [Get("/api/v1/clients/{clientId}")]
        Task<IApiResponse<ClientResponse>> GetClientAsync(System.Guid clientId, CancellationToken cancellationToken = default);

        [Headers("Accept: application/problem+json, application/json", "Content-Type: application/json")]
        [Put("/api/v1/clients/{clientId}")]
        Task<IApiResponse<ClientResponse>> UpdateClientAsync(System.Guid clientId, [Body] UpdateClientRequest body, [Header("If-Match")] string if_Match, CancellationToken cancellationToken = default);

        [Headers("Accept: application/json, application/problem+json")]
        [Get("/api/v1/devices/{deviceId}")]
        Task<IApiResponse<DeviceResponse>> GetDeviceAsync(System.Guid deviceId, CancellationToken cancellationToken = default);

        [Headers("Accept: application/problem+json, application/json", "Content-Type: application/json")]
        [Put("/api/v1/devices/{deviceId}")]
        Task<IApiResponse<DeviceResponse>> UpdateDeviceAsync(System.Guid deviceId, [Body] UpdateDeviceRequest body, [Header("If-Match")] string if_Match, CancellationToken cancellationToken = default);

        [Headers("Accept: application/problem+json, application/json")]
        [Get("/api/v1/devices/{deviceId}/work-orders")]
        Task<IApiResponse<PagedResponseOfDeviceWorkOrderHistoryItemResponse>> ListDeviceWorkOrdersAsync(System.Guid deviceId, [Query] int? page, [Query] int? pageSize, CancellationToken cancellationToken = default);

        [Headers("Accept: application/problem+json, application/json", "Content-Type: application/json")]
        [Post("/api/v1/work-orders")]
        Task<IApiResponse<WorkOrderResponse>> CreateWorkOrderAsync([Body] CreateWorkOrderRequest body, CancellationToken cancellationToken = default);

        [Headers("Accept: application/json, application/problem+json")]
        [Get("/api/v1/work-orders")]
        Task<IApiResponse<PagedResponseOfWorkOrderListItemResponse>> ListWorkOrdersAsync([Query] int? page, [Query] int? pageSize, [Query] WorkOrderStatus? status, [Query] System.Guid? technicianId, [Query] System.Guid? deviceId, [Query] System.Guid? clientId, [Query] bool? isOverdue, [Query(Format = "yyyy-MM-dd")] System.DateOnly? dueFrom, [Query(Format = "yyyy-MM-dd")] System.DateOnly? dueTo, [Query] string? search, [Query] SortBy? sortBy, [Query] SortDirection? sortDirection, CancellationToken cancellationToken = default);

        [Headers("Accept: application/json, application/problem+json")]
        [Get("/api/v1/work-orders/{workOrderId}")]
        Task<IApiResponse<WorkOrderResponse>> GetWorkOrderAsync(System.Guid workOrderId, CancellationToken cancellationToken = default);

        [Headers("Accept: application/problem+json, application/json", "Content-Type: application/json")]
        [Put("/api/v1/work-orders/{workOrderId}")]
        Task<IApiResponse<WorkOrderResponse>> UpdateWorkOrderAsync(System.Guid workOrderId, [Body] UpdateWorkOrderRequest body, [Header("If-Match")] string if_Match, CancellationToken cancellationToken = default);

        [Headers("Accept: application/json, application/problem+json")]
        [Post("/api/v1/work-orders/{workOrderId}/start")]
        Task<IApiResponse<WorkOrderResponse>> StartWorkAsync(System.Guid workOrderId, CancellationToken cancellationToken = default);

        [Headers("Accept: application/json, application/problem+json")]
        [Post("/api/v1/work-orders/{workOrderId}/complete")]
        Task<IApiResponse<WorkOrderResponse>> CompleteWorkOrderAsync(System.Guid workOrderId, CancellationToken cancellationToken = default);

        [Headers("Accept: application/pdf, application/problem+json")]
        [Get("/api/v1/work-orders/{workOrderId}/protocol")]
        Task<IApiResponse<System.IO.Stream>> GetServiceProtocolAsync(System.Guid workOrderId, CancellationToken cancellationToken = default);

        [Headers("Accept: application/problem+json, application/json", "Content-Type: application/json")]
        [Post("/api/v1/parts")]
        Task<IApiResponse<PartResponse>> CreatePartAsync([Body] CreatePartRequest body, CancellationToken cancellationToken = default);

        [Headers("Accept: application/json, application/problem+json")]
        [Get("/api/v1/parts")]
        Task<IApiResponse<PagedResponseOfPartResponse>> ListPartsAsync([Query] int? page, [Query] int? pageSize, [Query] string? search, CancellationToken cancellationToken = default);

        [Headers("Accept: application/json, application/problem+json")]
        [Get("/api/v1/parts/{partId}")]
        Task<IApiResponse<PartResponse>> GetPartAsync(System.Guid partId, CancellationToken cancellationToken = default);

        [Headers("Accept: application/problem+json, application/json", "Content-Type: application/json")]
        [Put("/api/v1/parts/{partId}")]
        Task<IApiResponse<PartResponse>> UpdatePartAsync(System.Guid partId, [Body] UpdatePartRequest body, [Header("If-Match")] string if_Match, CancellationToken cancellationToken = default);

        [Headers("Accept: application/problem+json, application/json", "Content-Type: application/json")]
        [Post("/api/v1/work-orders/{workOrderId}/service-entries")]
        Task<IApiResponse<ServiceEntryResponse>> AddServiceEntryAsync(System.Guid workOrderId, [Body] AddServiceEntryRequest body, CancellationToken cancellationToken = default);

        [Headers("Accept: application/json, application/problem+json")]
        [Get("/api/v1/work-orders/{workOrderId}/service-entries")]
        Task<IApiResponse<ICollection<ServiceEntryResponse>>> ListServiceEntriesAsync(System.Guid workOrderId, CancellationToken cancellationToken = default);

    }


}

#nullable enable

#pragma warning disable 108
#pragma warning disable 114
#pragma warning disable 472
#pragma warning disable 612
#pragma warning disable 649
#pragma warning disable 1573
#pragma warning disable 1591
#pragma warning disable 8073
#pragma warning disable 3016
#pragma warning disable 8600
#pragma warning disable 8602
#pragma warning disable 8603
#pragma warning disable 8604
#pragma warning disable 8625
#pragma warning disable 8765

namespace FixFlow.Mobile.Core.Api
{
    using System = global::System;

    

    [System.CodeDom.Compiler.GeneratedCode("NJsonSchema", "14.7.1.0 (NJsonSchema v11.6.1.0 (Newtonsoft.Json v13.0.0.0))")]
    public partial record AddServiceEntryRequest
    {
        [JsonConstructor]
        public AddServiceEntryRequest(System.Guid? @id, bool? @isCorrection, double? @latitude, double? @longitude, string @note, ICollection<ServiceEntryPartRequest>? @parts, ICollection<string>? @photoUrls, System.DateTimeOffset? @workFinishedAt, System.DateTimeOffset? @workStartedAt)
        {
            this.Note = @note;
            this.IsCorrection = @isCorrection;
            this.PhotoUrls = @photoUrls;
            this.WorkStartedAt = @workStartedAt;
            this.WorkFinishedAt = @workFinishedAt;
            this.Latitude = @latitude;
            this.Longitude = @longitude;
            this.Parts = @parts;
            this.Id = @id;
        }

        [JsonPropertyName("note")]
        [System.ComponentModel.DataAnnotations.Required(AllowEmptyStrings = true)]
        public string Note { get; init; }

        [JsonPropertyName("isCorrection")]
        public bool? IsCorrection { get; init; }

        [JsonPropertyName("photoUrls")]
        public ICollection<string>? PhotoUrls { get; init; }

        [JsonPropertyName("workStartedAt")]
        public System.DateTimeOffset? WorkStartedAt { get; init; }

        [JsonPropertyName("workFinishedAt")]
        public System.DateTimeOffset? WorkFinishedAt { get; init; }

        [JsonPropertyName("latitude")]
        public double? Latitude { get; init; }

        [JsonPropertyName("longitude")]
        public double? Longitude { get; init; }

        [JsonPropertyName("parts")]
        public ICollection<ServiceEntryPartRequest>? Parts { get; init; }

        [JsonPropertyName("id")]
        public System.Guid? Id { get; init; }

        private IDictionary<string, object>? _additionalProperties;

        [JsonExtensionData]
        public IDictionary<string, object> AdditionalProperties
        {
            get { return _additionalProperties ?? (_additionalProperties = new Dictionary<string, object>()); }
            set { _additionalProperties = value; }
        }

    }

    [System.CodeDom.Compiler.GeneratedCode("NJsonSchema", "14.7.1.0 (NJsonSchema v11.6.1.0 (Newtonsoft.Json v13.0.0.0))")]
    public partial record AuthTokensResponse
    {
        [JsonConstructor]
        public AuthTokensResponse(string @accessToken, System.DateTimeOffset @accessTokenExpiresAt, string @refreshToken, System.DateTimeOffset @refreshTokenExpiresAt)
        {
            this.AccessToken = @accessToken;
            this.AccessTokenExpiresAt = @accessTokenExpiresAt;
            this.RefreshToken = @refreshToken;
            this.RefreshTokenExpiresAt = @refreshTokenExpiresAt;
        }

        [JsonPropertyName("accessToken")]
        [System.ComponentModel.DataAnnotations.Required(AllowEmptyStrings = true)]
        public string AccessToken { get; init; }

        [JsonPropertyName("accessTokenExpiresAt")]
        [System.ComponentModel.DataAnnotations.Required(AllowEmptyStrings = true)]
        public System.DateTimeOffset AccessTokenExpiresAt { get; init; }

        [JsonPropertyName("refreshToken")]
        [System.ComponentModel.DataAnnotations.Required(AllowEmptyStrings = true)]
        public string RefreshToken { get; init; }

        [JsonPropertyName("refreshTokenExpiresAt")]
        [System.ComponentModel.DataAnnotations.Required(AllowEmptyStrings = true)]
        public System.DateTimeOffset RefreshTokenExpiresAt { get; init; }

        private IDictionary<string, object>? _additionalProperties;

        [JsonExtensionData]
        public IDictionary<string, object> AdditionalProperties
        {
            get { return _additionalProperties ?? (_additionalProperties = new Dictionary<string, object>()); }
            set { _additionalProperties = value; }
        }

    }

    [System.CodeDom.Compiler.GeneratedCode("NJsonSchema", "14.7.1.0 (NJsonSchema v11.6.1.0 (Newtonsoft.Json v13.0.0.0))")]
    public partial record ClientAddress
    {
        [JsonConstructor]
        public ClientAddress(string @buildingNumber, string @city, string @postalCode, string @street)
        {
            this.Street = @street;
            this.BuildingNumber = @buildingNumber;
            this.PostalCode = @postalCode;
            this.City = @city;
        }

        [JsonPropertyName("street")]
        [System.ComponentModel.DataAnnotations.Required(AllowEmptyStrings = true)]
        public string Street { get; init; }

        [JsonPropertyName("buildingNumber")]
        [System.ComponentModel.DataAnnotations.Required(AllowEmptyStrings = true)]
        public string BuildingNumber { get; init; }

        [JsonPropertyName("postalCode")]
        [System.ComponentModel.DataAnnotations.Required(AllowEmptyStrings = true)]
        public string PostalCode { get; init; }

        [JsonPropertyName("city")]
        [System.ComponentModel.DataAnnotations.Required(AllowEmptyStrings = true)]
        public string City { get; init; }

        private IDictionary<string, object>? _additionalProperties;

        [JsonExtensionData]
        public IDictionary<string, object> AdditionalProperties
        {
            get { return _additionalProperties ?? (_additionalProperties = new Dictionary<string, object>()); }
            set { _additionalProperties = value; }
        }

    }

    [System.CodeDom.Compiler.GeneratedCode("NJsonSchema", "14.7.1.0 (NJsonSchema v11.6.1.0 (Newtonsoft.Json v13.0.0.0))")]
    public partial record ClientResponse
    {
        [JsonConstructor]
        public ClientResponse(ClientAddress @address, System.DateTimeOffset? @archivedAt, string @contactPerson, System.DateTimeOffset @createdAt, string? @email, System.Guid @id, string @name, string @phone)
        {
            this.Id = @id;
            this.Name = @name;
            this.Address = @address;
            this.ContactPerson = @contactPerson;
            this.Phone = @phone;
            this.Email = @email;
            this.CreatedAt = @createdAt;
            this.ArchivedAt = @archivedAt;
        }

        [JsonPropertyName("id")]
        [System.ComponentModel.DataAnnotations.Required(AllowEmptyStrings = true)]
        public System.Guid Id { get; init; }

        [JsonPropertyName("name")]
        [System.ComponentModel.DataAnnotations.Required(AllowEmptyStrings = true)]
        public string Name { get; init; }

        [JsonPropertyName("address")]
        [System.ComponentModel.DataAnnotations.Required]
        public ClientAddress Address { get; init; }

        [JsonPropertyName("contactPerson")]
        [System.ComponentModel.DataAnnotations.Required(AllowEmptyStrings = true)]
        public string ContactPerson { get; init; }

        [JsonPropertyName("phone")]
        [System.ComponentModel.DataAnnotations.Required(AllowEmptyStrings = true)]
        public string Phone { get; init; }

        [JsonPropertyName("email")]
        public string? Email { get; init; }

        [JsonPropertyName("createdAt")]
        [System.ComponentModel.DataAnnotations.Required(AllowEmptyStrings = true)]
        public System.DateTimeOffset CreatedAt { get; init; }

        [JsonPropertyName("archivedAt")]
        public System.DateTimeOffset? ArchivedAt { get; init; }

        private IDictionary<string, object>? _additionalProperties;

        [JsonExtensionData]
        public IDictionary<string, object> AdditionalProperties
        {
            get { return _additionalProperties ?? (_additionalProperties = new Dictionary<string, object>()); }
            set { _additionalProperties = value; }
        }

    }

    [System.CodeDom.Compiler.GeneratedCode("NJsonSchema", "14.7.1.0 (NJsonSchema v11.6.1.0 (Newtonsoft.Json v13.0.0.0))")]
    public partial record CreatePartRequest
    {
        [JsonConstructor]
        public CreatePartRequest(string @catalogNumber, string @name, int @stockQuantity, decimal @unitPrice)
        {
            this.Name = @name;
            this.CatalogNumber = @catalogNumber;
            this.StockQuantity = @stockQuantity;
            this.UnitPrice = @unitPrice;
        }

        [JsonPropertyName("name")]
        [System.ComponentModel.DataAnnotations.Required(AllowEmptyStrings = true)]
        public string Name { get; init; }

        [JsonPropertyName("catalogNumber")]
        [System.ComponentModel.DataAnnotations.Required(AllowEmptyStrings = true)]
        public string CatalogNumber { get; init; }

        [JsonPropertyName("stockQuantity")]
        public int StockQuantity { get; init; }

        [JsonPropertyName("unitPrice")]
        public decimal UnitPrice { get; init; }

        private IDictionary<string, object>? _additionalProperties;

        [JsonExtensionData]
        public IDictionary<string, object> AdditionalProperties
        {
            get { return _additionalProperties ?? (_additionalProperties = new Dictionary<string, object>()); }
            set { _additionalProperties = value; }
        }

    }

    [System.CodeDom.Compiler.GeneratedCode("NJsonSchema", "14.7.1.0 (NJsonSchema v11.6.1.0 (Newtonsoft.Json v13.0.0.0))")]
    public partial record CreateWorkOrderRequest
    {
        [JsonConstructor]
        public CreateWorkOrderRequest(string @description, System.Guid @deviceId, System.DateTimeOffset @dueDate, WorkOrderPriority? @priority)
        {
            this.DeviceId = @deviceId;
            this.Description = @description;
            this.DueDate = @dueDate;
            this.Priority = @priority;
        }

        [JsonPropertyName("deviceId")]
        [System.ComponentModel.DataAnnotations.Required(AllowEmptyStrings = true)]
        public System.Guid DeviceId { get; init; }

        [JsonPropertyName("description")]
        [System.ComponentModel.DataAnnotations.Required(AllowEmptyStrings = true)]
        public string Description { get; init; }

        [JsonPropertyName("dueDate")]
        [System.ComponentModel.DataAnnotations.Required(AllowEmptyStrings = true)]
        public System.DateTimeOffset DueDate { get; init; }

        [JsonPropertyName("priority")]
public WorkOrderPriority? Priority { get; init; }

        private IDictionary<string, object>? _additionalProperties;

        [JsonExtensionData]
        public IDictionary<string, object> AdditionalProperties
        {
            get { return _additionalProperties ?? (_additionalProperties = new Dictionary<string, object>()); }
            set { _additionalProperties = value; }
        }

    }

    [System.CodeDom.Compiler.GeneratedCode("NJsonSchema", "14.7.1.0 (NJsonSchema v11.6.1.0 (Newtonsoft.Json v13.0.0.0))")]
    public partial record DeviceResponse
    {
        [JsonConstructor]
        public DeviceResponse(System.DateTimeOffset? @archivedAt, System.Guid @clientId, System.DateTimeOffset @createdAt, System.Guid @id, System.DateOnly @installationDate, string @manufacturer, string @model, string @serialNumber)
        {
            this.Id = @id;
            this.ClientId = @clientId;
            this.SerialNumber = @serialNumber;
            this.Model = @model;
            this.Manufacturer = @manufacturer;
            this.InstallationDate = @installationDate;
            this.CreatedAt = @createdAt;
            this.ArchivedAt = @archivedAt;
        }

        [JsonPropertyName("id")]
        [System.ComponentModel.DataAnnotations.Required(AllowEmptyStrings = true)]
        public System.Guid Id { get; init; }

        [JsonPropertyName("clientId")]
        [System.ComponentModel.DataAnnotations.Required(AllowEmptyStrings = true)]
        public System.Guid ClientId { get; init; }

        [JsonPropertyName("serialNumber")]
        [System.ComponentModel.DataAnnotations.Required(AllowEmptyStrings = true)]
        public string SerialNumber { get; init; }

        [JsonPropertyName("model")]
        [System.ComponentModel.DataAnnotations.Required(AllowEmptyStrings = true)]
        public string Model { get; init; }

        [JsonPropertyName("manufacturer")]
        [System.ComponentModel.DataAnnotations.Required(AllowEmptyStrings = true)]
        public string Manufacturer { get; init; }

        [JsonPropertyName("installationDate")]
        [System.ComponentModel.DataAnnotations.Required(AllowEmptyStrings = true)]
        public System.DateOnly InstallationDate { get; init; }

        [JsonPropertyName("createdAt")]
        [System.ComponentModel.DataAnnotations.Required(AllowEmptyStrings = true)]
        public System.DateTimeOffset CreatedAt { get; init; }

        [JsonPropertyName("archivedAt")]
        public System.DateTimeOffset? ArchivedAt { get; init; }

        private IDictionary<string, object>? _additionalProperties;

        [JsonExtensionData]
        public IDictionary<string, object> AdditionalProperties
        {
            get { return _additionalProperties ?? (_additionalProperties = new Dictionary<string, object>()); }
            set { _additionalProperties = value; }
        }

    }

    [System.CodeDom.Compiler.GeneratedCode("NJsonSchema", "14.7.1.0 (NJsonSchema v11.6.1.0 (Newtonsoft.Json v13.0.0.0))")]
    public partial record DeviceWorkOrderHistoryItemResponse
    {
        [JsonConstructor]
        public DeviceWorkOrderHistoryItemResponse(System.DateTimeOffset? @completedAt, System.DateTimeOffset @createdAt, string @description, System.Guid @id, string @number, WorkOrderPriority @priority, int @serviceEntryCount, System.DateTimeOffset? @startedAt, WorkOrderStatus @status, string? @technicianName)
        {
            this.Id = @id;
            this.Number = @number;
            this.Status = @status;
            this.Priority = @priority;
            this.Description = @description;
            this.TechnicianName = @technicianName;
            this.CreatedAt = @createdAt;
            this.StartedAt = @startedAt;
            this.CompletedAt = @completedAt;
            this.ServiceEntryCount = @serviceEntryCount;
        }

        [JsonPropertyName("id")]
        [System.ComponentModel.DataAnnotations.Required(AllowEmptyStrings = true)]
        public System.Guid Id { get; init; }

        [JsonPropertyName("number")]
        [System.ComponentModel.DataAnnotations.Required(AllowEmptyStrings = true)]
        public string Number { get; init; }

        [JsonPropertyName("status")]
        [System.ComponentModel.DataAnnotations.Required(AllowEmptyStrings = true)]
public WorkOrderStatus Status { get; init; }

        [JsonPropertyName("priority")]
        [System.ComponentModel.DataAnnotations.Required(AllowEmptyStrings = true)]
public WorkOrderPriority Priority { get; init; }

        [JsonPropertyName("description")]
        [System.ComponentModel.DataAnnotations.Required(AllowEmptyStrings = true)]
        public string Description { get; init; }

        [JsonPropertyName("technicianName")]
        public string? TechnicianName { get; init; }

        [JsonPropertyName("createdAt")]
        [System.ComponentModel.DataAnnotations.Required(AllowEmptyStrings = true)]
        public System.DateTimeOffset CreatedAt { get; init; }

        [JsonPropertyName("startedAt")]
        public System.DateTimeOffset? StartedAt { get; init; }

        [JsonPropertyName("completedAt")]
        public System.DateTimeOffset? CompletedAt { get; init; }

        [JsonPropertyName("serviceEntryCount")]
        public int ServiceEntryCount { get; init; }

        private IDictionary<string, object>? _additionalProperties;

        [JsonExtensionData]
        public IDictionary<string, object> AdditionalProperties
        {
            get { return _additionalProperties ?? (_additionalProperties = new Dictionary<string, object>()); }
            set { _additionalProperties = value; }
        }

    }

    [System.CodeDom.Compiler.GeneratedCode("NJsonSchema", "14.7.1.0 (NJsonSchema v11.6.1.0 (Newtonsoft.Json v13.0.0.0))")]
    public partial record HttpValidationProblemDetails
    {
        [JsonConstructor]
        public HttpValidationProblemDetails(string? @detail, IDictionary<string, ICollection<string>>? @errors, string? @instance, int? @status, string? @title, string? @type)
        {
            this.Type = @type;
            this.Title = @title;
            this.Status = @status;
            this.Detail = @detail;
            this.Instance = @instance;
            this.Errors = @errors;
        }

        [JsonPropertyName("type")]
        public string? Type { get; init; }

        [JsonPropertyName("title")]
        public string? Title { get; init; }

        [JsonPropertyName("status")]
        public int? Status { get; init; }

        [JsonPropertyName("detail")]
        public string? Detail { get; init; }

        [JsonPropertyName("instance")]
        public string? Instance { get; init; }

        [JsonPropertyName("errors")]
        public IDictionary<string, ICollection<string>>? Errors { get; init; }

        private IDictionary<string, object>? _additionalProperties;

        [JsonExtensionData]
        public IDictionary<string, object> AdditionalProperties
        {
            get { return _additionalProperties ?? (_additionalProperties = new Dictionary<string, object>()); }
            set { _additionalProperties = value; }
        }

    }

    [System.CodeDom.Compiler.GeneratedCode("NJsonSchema", "14.7.1.0 (NJsonSchema v11.6.1.0 (Newtonsoft.Json v13.0.0.0))")]
    public partial record LoginRequest
    {
        [JsonConstructor]
        public LoginRequest(string @email, string @password)
        {
            this.Email = @email;
            this.Password = @password;
        }

        [JsonPropertyName("email")]
        [System.ComponentModel.DataAnnotations.Required(AllowEmptyStrings = true)]
        public string Email { get; init; }

        [JsonPropertyName("password")]
        [System.ComponentModel.DataAnnotations.Required(AllowEmptyStrings = true)]
        public string Password { get; init; }

        private IDictionary<string, object>? _additionalProperties;

        [JsonExtensionData]
        public IDictionary<string, object> AdditionalProperties
        {
            get { return _additionalProperties ?? (_additionalProperties = new Dictionary<string, object>()); }
            set { _additionalProperties = value; }
        }

    }

    [System.CodeDom.Compiler.GeneratedCode("NJsonSchema", "14.7.1.0 (NJsonSchema v11.6.1.0 (Newtonsoft.Json v13.0.0.0))")]
    public partial record LogoutRequest
    {
        [JsonConstructor]
        public LogoutRequest(string @refreshToken)
        {
            this.RefreshToken = @refreshToken;
        }

        [JsonPropertyName("refreshToken")]
        [System.ComponentModel.DataAnnotations.Required(AllowEmptyStrings = true)]
        public string RefreshToken { get; init; }

        private IDictionary<string, object>? _additionalProperties;

        [JsonExtensionData]
        public IDictionary<string, object> AdditionalProperties
        {
            get { return _additionalProperties ?? (_additionalProperties = new Dictionary<string, object>()); }
            set { _additionalProperties = value; }
        }

    }

    [System.CodeDom.Compiler.GeneratedCode("NJsonSchema", "14.7.1.0 (NJsonSchema v11.6.1.0 (Newtonsoft.Json v13.0.0.0))")]
    public partial record PagedResponseOfDeviceWorkOrderHistoryItemResponse
    {
        [JsonConstructor]
        public PagedResponseOfDeviceWorkOrderHistoryItemResponse(ICollection<DeviceWorkOrderHistoryItemResponse> @items, int @page, int @pageSize, int @totalCount)
        {
            this.Items = @items;
            this.Page = @page;
            this.PageSize = @pageSize;
            this.TotalCount = @totalCount;
        }

        [JsonPropertyName("items")]
        [System.ComponentModel.DataAnnotations.Required]
        public ICollection<DeviceWorkOrderHistoryItemResponse> Items { get; init; }

        [JsonPropertyName("page")]
        public int Page { get; init; }

        [JsonPropertyName("pageSize")]
        public int PageSize { get; init; }

        [JsonPropertyName("totalCount")]
        public int TotalCount { get; init; }

        private IDictionary<string, object>? _additionalProperties;

        [JsonExtensionData]
        public IDictionary<string, object> AdditionalProperties
        {
            get { return _additionalProperties ?? (_additionalProperties = new Dictionary<string, object>()); }
            set { _additionalProperties = value; }
        }

    }

    [System.CodeDom.Compiler.GeneratedCode("NJsonSchema", "14.7.1.0 (NJsonSchema v11.6.1.0 (Newtonsoft.Json v13.0.0.0))")]
    public partial record PagedResponseOfPartResponse
    {
        [JsonConstructor]
        public PagedResponseOfPartResponse(ICollection<PartResponse> @items, int @page, int @pageSize, int @totalCount)
        {
            this.Items = @items;
            this.Page = @page;
            this.PageSize = @pageSize;
            this.TotalCount = @totalCount;
        }

        [JsonPropertyName("items")]
        [System.ComponentModel.DataAnnotations.Required]
        public ICollection<PartResponse> Items { get; init; }

        [JsonPropertyName("page")]
        public int Page { get; init; }

        [JsonPropertyName("pageSize")]
        public int PageSize { get; init; }

        [JsonPropertyName("totalCount")]
        public int TotalCount { get; init; }

        private IDictionary<string, object>? _additionalProperties;

        [JsonExtensionData]
        public IDictionary<string, object> AdditionalProperties
        {
            get { return _additionalProperties ?? (_additionalProperties = new Dictionary<string, object>()); }
            set { _additionalProperties = value; }
        }

    }

    [System.CodeDom.Compiler.GeneratedCode("NJsonSchema", "14.7.1.0 (NJsonSchema v11.6.1.0 (Newtonsoft.Json v13.0.0.0))")]
    public partial record PagedResponseOfWorkOrderListItemResponse
    {
        [JsonConstructor]
        public PagedResponseOfWorkOrderListItemResponse(ICollection<WorkOrderListItemResponse> @items, int @page, int @pageSize, int @totalCount)
        {
            this.Items = @items;
            this.Page = @page;
            this.PageSize = @pageSize;
            this.TotalCount = @totalCount;
        }

        [JsonPropertyName("items")]
        [System.ComponentModel.DataAnnotations.Required]
        public ICollection<WorkOrderListItemResponse> Items { get; init; }

        [JsonPropertyName("page")]
        public int Page { get; init; }

        [JsonPropertyName("pageSize")]
        public int PageSize { get; init; }

        [JsonPropertyName("totalCount")]
        public int TotalCount { get; init; }

        private IDictionary<string, object>? _additionalProperties;

        [JsonExtensionData]
        public IDictionary<string, object> AdditionalProperties
        {
            get { return _additionalProperties ?? (_additionalProperties = new Dictionary<string, object>()); }
            set { _additionalProperties = value; }
        }

    }

    [System.CodeDom.Compiler.GeneratedCode("NJsonSchema", "14.7.1.0 (NJsonSchema v11.6.1.0 (Newtonsoft.Json v13.0.0.0))")]
    public partial record PartResponse
    {
        [JsonConstructor]
        public PartResponse(System.DateTimeOffset? @archivedAt, string @catalogNumber, System.DateTimeOffset @createdAt, System.Guid @id, string @name, int @stockQuantity, decimal @unitPrice)
        {
            this.Id = @id;
            this.Name = @name;
            this.CatalogNumber = @catalogNumber;
            this.StockQuantity = @stockQuantity;
            this.UnitPrice = @unitPrice;
            this.CreatedAt = @createdAt;
            this.ArchivedAt = @archivedAt;
        }

        [JsonPropertyName("id")]
        [System.ComponentModel.DataAnnotations.Required(AllowEmptyStrings = true)]
        public System.Guid Id { get; init; }

        [JsonPropertyName("name")]
        [System.ComponentModel.DataAnnotations.Required(AllowEmptyStrings = true)]
        public string Name { get; init; }

        [JsonPropertyName("catalogNumber")]
        [System.ComponentModel.DataAnnotations.Required(AllowEmptyStrings = true)]
        public string CatalogNumber { get; init; }

        [JsonPropertyName("stockQuantity")]
        public int StockQuantity { get; init; }

        [JsonPropertyName("unitPrice")]
        public decimal UnitPrice { get; init; }

        [JsonPropertyName("createdAt")]
        [System.ComponentModel.DataAnnotations.Required(AllowEmptyStrings = true)]
        public System.DateTimeOffset CreatedAt { get; init; }

        [JsonPropertyName("archivedAt")]
        public System.DateTimeOffset? ArchivedAt { get; init; }

        private IDictionary<string, object>? _additionalProperties;

        [JsonExtensionData]
        public IDictionary<string, object> AdditionalProperties
        {
            get { return _additionalProperties ?? (_additionalProperties = new Dictionary<string, object>()); }
            set { _additionalProperties = value; }
        }

    }

    [System.CodeDom.Compiler.GeneratedCode("NJsonSchema", "14.7.1.0 (NJsonSchema v11.6.1.0 (Newtonsoft.Json v13.0.0.0))")]
    public partial record ProblemDetails
    {
        [JsonConstructor]
        public ProblemDetails(string? @detail, string? @errorCode, string? @instance, int? @status, string? @title, string? @type)
        {
            this.Type = @type;
            this.Title = @title;
            this.Status = @status;
            this.Detail = @detail;
            this.Instance = @instance;
            this.ErrorCode = @errorCode;
        }

        [JsonPropertyName("type")]
        public string? Type { get; init; }

        [JsonPropertyName("title")]
        public string? Title { get; init; }

        [JsonPropertyName("status")]
        public int? Status { get; init; }

        [JsonPropertyName("detail")]
        public string? Detail { get; init; }

        [JsonPropertyName("instance")]
        public string? Instance { get; init; }

        [JsonPropertyName("errorCode")]
        public string? ErrorCode { get; init; }

        private IDictionary<string, object>? _additionalProperties;

        [JsonExtensionData]
        public IDictionary<string, object> AdditionalProperties
        {
            get { return _additionalProperties ?? (_additionalProperties = new Dictionary<string, object>()); }
            set { _additionalProperties = value; }
        }

    }

    [System.CodeDom.Compiler.GeneratedCode("NJsonSchema", "14.7.1.0 (NJsonSchema v11.6.1.0 (Newtonsoft.Json v13.0.0.0))")]
    public partial record RefreshRequest
    {
        [JsonConstructor]
        public RefreshRequest(string @refreshToken)
        {
            this.RefreshToken = @refreshToken;
        }

        [JsonPropertyName("refreshToken")]
        [System.ComponentModel.DataAnnotations.Required(AllowEmptyStrings = true)]
        public string RefreshToken { get; init; }

        private IDictionary<string, object>? _additionalProperties;

        [JsonExtensionData]
        public IDictionary<string, object> AdditionalProperties
        {
            get { return _additionalProperties ?? (_additionalProperties = new Dictionary<string, object>()); }
            set { _additionalProperties = value; }
        }

    }

    [System.CodeDom.Compiler.GeneratedCode("NJsonSchema", "14.7.1.0 (NJsonSchema v11.6.1.0 (Newtonsoft.Json v13.0.0.0))")]
    public partial record ServiceEntryPartRequest
    {
        [JsonConstructor]
        public ServiceEntryPartRequest(System.Guid @partId, int @quantity)
        {
            this.PartId = @partId;
            this.Quantity = @quantity;
        }

        [JsonPropertyName("partId")]
        [System.ComponentModel.DataAnnotations.Required(AllowEmptyStrings = true)]
        public System.Guid PartId { get; init; }

        [JsonPropertyName("quantity")]
        public int Quantity { get; init; }

        private IDictionary<string, object>? _additionalProperties;

        [JsonExtensionData]
        public IDictionary<string, object> AdditionalProperties
        {
            get { return _additionalProperties ?? (_additionalProperties = new Dictionary<string, object>()); }
            set { _additionalProperties = value; }
        }

    }

    [System.CodeDom.Compiler.GeneratedCode("NJsonSchema", "14.7.1.0 (NJsonSchema v11.6.1.0 (Newtonsoft.Json v13.0.0.0))")]
    public partial record ServiceEntryPartResponse
    {
        [JsonConstructor]
        public ServiceEntryPartResponse(string @catalogNumber, System.Guid @partId, string @partName, int @quantity, decimal @unitPrice)
        {
            this.PartId = @partId;
            this.PartName = @partName;
            this.CatalogNumber = @catalogNumber;
            this.Quantity = @quantity;
            this.UnitPrice = @unitPrice;
        }

        [JsonPropertyName("partId")]
        [System.ComponentModel.DataAnnotations.Required(AllowEmptyStrings = true)]
        public System.Guid PartId { get; init; }

        [JsonPropertyName("partName")]
        [System.ComponentModel.DataAnnotations.Required(AllowEmptyStrings = true)]
        public string PartName { get; init; }

        [JsonPropertyName("catalogNumber")]
        [System.ComponentModel.DataAnnotations.Required(AllowEmptyStrings = true)]
        public string CatalogNumber { get; init; }

        [JsonPropertyName("quantity")]
        public int Quantity { get; init; }

        [JsonPropertyName("unitPrice")]
        public decimal UnitPrice { get; init; }

        private IDictionary<string, object>? _additionalProperties;

        [JsonExtensionData]
        public IDictionary<string, object> AdditionalProperties
        {
            get { return _additionalProperties ?? (_additionalProperties = new Dictionary<string, object>()); }
            set { _additionalProperties = value; }
        }

    }

    [System.CodeDom.Compiler.GeneratedCode("NJsonSchema", "14.7.1.0 (NJsonSchema v11.6.1.0 (Newtonsoft.Json v13.0.0.0))")]
    public partial record ServiceEntryResponse
    {
        [JsonConstructor]
        public ServiceEntryResponse(System.DateTimeOffset @createdAt, System.Guid @id, bool @isCorrection, double? @latitude, double? @longitude, string @note, ICollection<ServiceEntryPartResponse> @parts, ICollection<string> @photoUrls, System.Guid @technicianId, string @technicianName, System.DateTimeOffset? @workFinishedAt, System.Guid @workOrderId, System.DateTimeOffset? @workStartedAt)
        {
            this.Id = @id;
            this.WorkOrderId = @workOrderId;
            this.TechnicianId = @technicianId;
            this.TechnicianName = @technicianName;
            this.Note = @note;
            this.IsCorrection = @isCorrection;
            this.PhotoUrls = @photoUrls;
            this.WorkStartedAt = @workStartedAt;
            this.WorkFinishedAt = @workFinishedAt;
            this.Latitude = @latitude;
            this.Longitude = @longitude;
            this.Parts = @parts;
            this.CreatedAt = @createdAt;
        }

        [JsonPropertyName("id")]
        [System.ComponentModel.DataAnnotations.Required(AllowEmptyStrings = true)]
        public System.Guid Id { get; init; }

        [JsonPropertyName("workOrderId")]
        [System.ComponentModel.DataAnnotations.Required(AllowEmptyStrings = true)]
        public System.Guid WorkOrderId { get; init; }

        [JsonPropertyName("technicianId")]
        [System.ComponentModel.DataAnnotations.Required(AllowEmptyStrings = true)]
        public System.Guid TechnicianId { get; init; }

        [JsonPropertyName("technicianName")]
        [System.ComponentModel.DataAnnotations.Required(AllowEmptyStrings = true)]
        public string TechnicianName { get; init; }

        [JsonPropertyName("note")]
        [System.ComponentModel.DataAnnotations.Required(AllowEmptyStrings = true)]
        public string Note { get; init; }

        [JsonPropertyName("isCorrection")]
        public bool IsCorrection { get; init; }

        [JsonPropertyName("photoUrls")]
        [System.ComponentModel.DataAnnotations.Required]
        public ICollection<string> PhotoUrls { get; init; }

        [JsonPropertyName("workStartedAt")]
        public System.DateTimeOffset? WorkStartedAt { get; init; }

        [JsonPropertyName("workFinishedAt")]
        public System.DateTimeOffset? WorkFinishedAt { get; init; }

        [JsonPropertyName("latitude")]
        public double? Latitude { get; init; }

        [JsonPropertyName("longitude")]
        public double? Longitude { get; init; }

        [JsonPropertyName("parts")]
        [System.ComponentModel.DataAnnotations.Required]
        public ICollection<ServiceEntryPartResponse> Parts { get; init; }

        [JsonPropertyName("createdAt")]
        [System.ComponentModel.DataAnnotations.Required(AllowEmptyStrings = true)]
        public System.DateTimeOffset CreatedAt { get; init; }

        private IDictionary<string, object>? _additionalProperties;

        [JsonExtensionData]
        public IDictionary<string, object> AdditionalProperties
        {
            get { return _additionalProperties ?? (_additionalProperties = new Dictionary<string, object>()); }
            set { _additionalProperties = value; }
        }

    }

    [System.CodeDom.Compiler.GeneratedCode("NJsonSchema", "14.7.1.0 (NJsonSchema v11.6.1.0 (Newtonsoft.Json v13.0.0.0))")]
    public partial record UpdateClientRequest
    {
        [JsonConstructor]
        public UpdateClientRequest(ClientAddress @address, string @contactPerson, string? @email, string @name, string @phone)
        {
            this.Name = @name;
            this.Address = @address;
            this.ContactPerson = @contactPerson;
            this.Phone = @phone;
            this.Email = @email;
        }

        [JsonPropertyName("name")]
        [System.ComponentModel.DataAnnotations.Required(AllowEmptyStrings = true)]
        public string Name { get; init; }

        [JsonPropertyName("address")]
        [System.ComponentModel.DataAnnotations.Required]
        public ClientAddress Address { get; init; }

        [JsonPropertyName("contactPerson")]
        [System.ComponentModel.DataAnnotations.Required(AllowEmptyStrings = true)]
        public string ContactPerson { get; init; }

        [JsonPropertyName("phone")]
        [System.ComponentModel.DataAnnotations.Required(AllowEmptyStrings = true)]
        public string Phone { get; init; }

        [JsonPropertyName("email")]
        public string? Email { get; init; }

        private IDictionary<string, object>? _additionalProperties;

        [JsonExtensionData]
        public IDictionary<string, object> AdditionalProperties
        {
            get { return _additionalProperties ?? (_additionalProperties = new Dictionary<string, object>()); }
            set { _additionalProperties = value; }
        }

    }

    [System.CodeDom.Compiler.GeneratedCode("NJsonSchema", "14.7.1.0 (NJsonSchema v11.6.1.0 (Newtonsoft.Json v13.0.0.0))")]
    public partial record UpdateDeviceRequest
    {
        [JsonConstructor]
        public UpdateDeviceRequest(System.DateOnly @installationDate, string @manufacturer, string @model, string @serialNumber)
        {
            this.SerialNumber = @serialNumber;
            this.Model = @model;
            this.Manufacturer = @manufacturer;
            this.InstallationDate = @installationDate;
        }

        [JsonPropertyName("serialNumber")]
        [System.ComponentModel.DataAnnotations.Required(AllowEmptyStrings = true)]
        public string SerialNumber { get; init; }

        [JsonPropertyName("model")]
        [System.ComponentModel.DataAnnotations.Required(AllowEmptyStrings = true)]
        public string Model { get; init; }

        [JsonPropertyName("manufacturer")]
        [System.ComponentModel.DataAnnotations.Required(AllowEmptyStrings = true)]
        public string Manufacturer { get; init; }

        [JsonPropertyName("installationDate")]
        [System.ComponentModel.DataAnnotations.Required(AllowEmptyStrings = true)]
        public System.DateOnly InstallationDate { get; init; }

        private IDictionary<string, object>? _additionalProperties;

        [JsonExtensionData]
        public IDictionary<string, object> AdditionalProperties
        {
            get { return _additionalProperties ?? (_additionalProperties = new Dictionary<string, object>()); }
            set { _additionalProperties = value; }
        }

    }

    [System.CodeDom.Compiler.GeneratedCode("NJsonSchema", "14.7.1.0 (NJsonSchema v11.6.1.0 (Newtonsoft.Json v13.0.0.0))")]
    public partial record UpdatePartRequest
    {
        [JsonConstructor]
        public UpdatePartRequest(string @catalogNumber, string @name, decimal @unitPrice)
        {
            this.Name = @name;
            this.CatalogNumber = @catalogNumber;
            this.UnitPrice = @unitPrice;
        }

        [JsonPropertyName("name")]
        [System.ComponentModel.DataAnnotations.Required(AllowEmptyStrings = true)]
        public string Name { get; init; }

        [JsonPropertyName("catalogNumber")]
        [System.ComponentModel.DataAnnotations.Required(AllowEmptyStrings = true)]
        public string CatalogNumber { get; init; }

        [JsonPropertyName("unitPrice")]
        public decimal UnitPrice { get; init; }

        private IDictionary<string, object>? _additionalProperties;

        [JsonExtensionData]
        public IDictionary<string, object> AdditionalProperties
        {
            get { return _additionalProperties ?? (_additionalProperties = new Dictionary<string, object>()); }
            set { _additionalProperties = value; }
        }

    }

    [System.CodeDom.Compiler.GeneratedCode("NJsonSchema", "14.7.1.0 (NJsonSchema v11.6.1.0 (Newtonsoft.Json v13.0.0.0))")]
    public partial record UpdateWorkOrderRequest
    {
        [JsonConstructor]
        public UpdateWorkOrderRequest(string @description, System.DateTimeOffset @dueDate, WorkOrderPriority @priority)
        {
            this.Description = @description;
            this.Priority = @priority;
            this.DueDate = @dueDate;
        }

        [JsonPropertyName("description")]
        [System.ComponentModel.DataAnnotations.Required(AllowEmptyStrings = true)]
        public string Description { get; init; }

        [JsonPropertyName("priority")]
        [System.ComponentModel.DataAnnotations.Required(AllowEmptyStrings = true)]
public WorkOrderPriority Priority { get; init; }

        [JsonPropertyName("dueDate")]
        [System.ComponentModel.DataAnnotations.Required(AllowEmptyStrings = true)]
        public System.DateTimeOffset DueDate { get; init; }

        private IDictionary<string, object>? _additionalProperties;

        [JsonExtensionData]
        public IDictionary<string, object> AdditionalProperties
        {
            get { return _additionalProperties ?? (_additionalProperties = new Dictionary<string, object>()); }
            set { _additionalProperties = value; }
        }

    }

    [System.CodeDom.Compiler.GeneratedCode("NJsonSchema", "14.7.1.0 (NJsonSchema v11.6.1.0 (Newtonsoft.Json v13.0.0.0))")]
    public partial record UserResponse
    {
        [JsonConstructor]
        public UserResponse(string @email, string @fullName, System.Guid @id, bool @isActive, string @role)
        {
            this.Id = @id;
            this.Email = @email;
            this.FullName = @fullName;
            this.Role = @role;
            this.IsActive = @isActive;
        }

        [JsonPropertyName("id")]
        [System.ComponentModel.DataAnnotations.Required(AllowEmptyStrings = true)]
        public System.Guid Id { get; init; }

        [JsonPropertyName("email")]
        [System.ComponentModel.DataAnnotations.Required(AllowEmptyStrings = true)]
        public string Email { get; init; }

        [JsonPropertyName("fullName")]
        [System.ComponentModel.DataAnnotations.Required(AllowEmptyStrings = true)]
        public string FullName { get; init; }

        [JsonPropertyName("role")]
        [System.ComponentModel.DataAnnotations.Required(AllowEmptyStrings = true)]
        public string Role { get; init; }

        [JsonPropertyName("isActive")]
        public bool IsActive { get; init; }

        private IDictionary<string, object>? _additionalProperties;

        [JsonExtensionData]
        public IDictionary<string, object> AdditionalProperties
        {
            get { return _additionalProperties ?? (_additionalProperties = new Dictionary<string, object>()); }
            set { _additionalProperties = value; }
        }

    }

    [System.CodeDom.Compiler.GeneratedCode("NJsonSchema", "14.7.1.0 (NJsonSchema v11.6.1.0 (Newtonsoft.Json v13.0.0.0))")]
    public partial record WorkOrderListItemResponse
    {
        [JsonConstructor]
        public WorkOrderListItemResponse(System.Guid @clientId, string @clientName, System.DateTimeOffset? @completedAt, System.DateTimeOffset @createdAt, string @description, System.Guid @deviceId, string @deviceModel, string @deviceSerialNumber, System.DateTimeOffset @dueDate, System.Guid @id, System.DateTimeOffset? @invoicedAt, bool @isOverdue, string @number, WorkOrderPriority @priority, System.DateTimeOffset? @startedAt, WorkOrderStatus @status, string? @technicianEmail, System.Guid? @technicianId, string? @technicianName)
        {
            this.Id = @id;
            this.Number = @number;
            this.DeviceId = @deviceId;
            this.DeviceSerialNumber = @deviceSerialNumber;
            this.DeviceModel = @deviceModel;
            this.ClientId = @clientId;
            this.ClientName = @clientName;
            this.Description = @description;
            this.Priority = @priority;
            this.Status = @status;
            this.TechnicianId = @technicianId;
            this.TechnicianEmail = @technicianEmail;
            this.TechnicianName = @technicianName;
            this.DueDate = @dueDate;
            this.IsOverdue = @isOverdue;
            this.CreatedAt = @createdAt;
            this.StartedAt = @startedAt;
            this.CompletedAt = @completedAt;
            this.InvoicedAt = @invoicedAt;
        }

        [JsonPropertyName("id")]
        [System.ComponentModel.DataAnnotations.Required(AllowEmptyStrings = true)]
        public System.Guid Id { get; init; }

        [JsonPropertyName("number")]
        [System.ComponentModel.DataAnnotations.Required(AllowEmptyStrings = true)]
        public string Number { get; init; }

        [JsonPropertyName("deviceId")]
        [System.ComponentModel.DataAnnotations.Required(AllowEmptyStrings = true)]
        public System.Guid DeviceId { get; init; }

        [JsonPropertyName("deviceSerialNumber")]
        [System.ComponentModel.DataAnnotations.Required(AllowEmptyStrings = true)]
        public string DeviceSerialNumber { get; init; }

        [JsonPropertyName("deviceModel")]
        [System.ComponentModel.DataAnnotations.Required(AllowEmptyStrings = true)]
        public string DeviceModel { get; init; }

        [JsonPropertyName("clientId")]
        [System.ComponentModel.DataAnnotations.Required(AllowEmptyStrings = true)]
        public System.Guid ClientId { get; init; }

        [JsonPropertyName("clientName")]
        [System.ComponentModel.DataAnnotations.Required(AllowEmptyStrings = true)]
        public string ClientName { get; init; }

        [JsonPropertyName("description")]
        [System.ComponentModel.DataAnnotations.Required(AllowEmptyStrings = true)]
        public string Description { get; init; }

        [JsonPropertyName("priority")]
        [System.ComponentModel.DataAnnotations.Required(AllowEmptyStrings = true)]
public WorkOrderPriority Priority { get; init; }

        [JsonPropertyName("status")]
        [System.ComponentModel.DataAnnotations.Required(AllowEmptyStrings = true)]
public WorkOrderStatus Status { get; init; }

        [JsonPropertyName("technicianId")]
        public System.Guid? TechnicianId { get; init; }

        [JsonPropertyName("technicianEmail")]
        public string? TechnicianEmail { get; init; }

        [JsonPropertyName("technicianName")]
        public string? TechnicianName { get; init; }

        [JsonPropertyName("dueDate")]
        [System.ComponentModel.DataAnnotations.Required(AllowEmptyStrings = true)]
        public System.DateTimeOffset DueDate { get; init; }

        [JsonPropertyName("isOverdue")]
        public bool IsOverdue { get; init; }

        [JsonPropertyName("createdAt")]
        [System.ComponentModel.DataAnnotations.Required(AllowEmptyStrings = true)]
        public System.DateTimeOffset CreatedAt { get; init; }

        [JsonPropertyName("startedAt")]
        public System.DateTimeOffset? StartedAt { get; init; }

        [JsonPropertyName("completedAt")]
        public System.DateTimeOffset? CompletedAt { get; init; }

        [JsonPropertyName("invoicedAt")]
        public System.DateTimeOffset? InvoicedAt { get; init; }

        private IDictionary<string, object>? _additionalProperties;

        [JsonExtensionData]
        public IDictionary<string, object> AdditionalProperties
        {
            get { return _additionalProperties ?? (_additionalProperties = new Dictionary<string, object>()); }
            set { _additionalProperties = value; }
        }

    }

    [System.CodeDom.Compiler.GeneratedCode("NJsonSchema", "14.7.1.0 (NJsonSchema v11.6.1.0 (Newtonsoft.Json v13.0.0.0))")]
    [JsonConverter(typeof(JsonStringEnumConverter<WorkOrderPriority>))]
    public enum WorkOrderPriority
    {

        [System.Runtime.Serialization.EnumMember(Value = @"Low")]
        Low = 0,

        [System.Runtime.Serialization.EnumMember(Value = @"Normal")]
        Normal = 1,

        [System.Runtime.Serialization.EnumMember(Value = @"High")]
        High = 2,

        [System.Runtime.Serialization.EnumMember(Value = @"Critical")]
        Critical = 3,

    }

    [System.CodeDom.Compiler.GeneratedCode("NJsonSchema", "14.7.1.0 (NJsonSchema v11.6.1.0 (Newtonsoft.Json v13.0.0.0))")]
    public partial record WorkOrderResponse
    {
        [JsonConstructor]
        public WorkOrderResponse(System.Guid @clientId, string @clientName, System.DateTimeOffset? @completedAt, System.DateTimeOffset @createdAt, string @description, System.Guid @deviceId, string @deviceModel, string @deviceSerialNumber, System.DateTimeOffset @dueDate, System.Guid @id, System.DateTimeOffset? @invoicedAt, bool @isOverdue, string @number, WorkOrderPriority @priority, System.DateTimeOffset? @startedAt, WorkOrderStatus @status, string? @technicianEmail, System.Guid? @technicianId, string? @technicianName)
        {
            this.Id = @id;
            this.Number = @number;
            this.DeviceId = @deviceId;
            this.DeviceSerialNumber = @deviceSerialNumber;
            this.DeviceModel = @deviceModel;
            this.ClientId = @clientId;
            this.ClientName = @clientName;
            this.Description = @description;
            this.Priority = @priority;
            this.Status = @status;
            this.TechnicianId = @technicianId;
            this.TechnicianEmail = @technicianEmail;
            this.TechnicianName = @technicianName;
            this.DueDate = @dueDate;
            this.IsOverdue = @isOverdue;
            this.CreatedAt = @createdAt;
            this.StartedAt = @startedAt;
            this.CompletedAt = @completedAt;
            this.InvoicedAt = @invoicedAt;
        }

        [JsonPropertyName("id")]
        [System.ComponentModel.DataAnnotations.Required(AllowEmptyStrings = true)]
        public System.Guid Id { get; init; }

        [JsonPropertyName("number")]
        [System.ComponentModel.DataAnnotations.Required(AllowEmptyStrings = true)]
        public string Number { get; init; }

        [JsonPropertyName("deviceId")]
        [System.ComponentModel.DataAnnotations.Required(AllowEmptyStrings = true)]
        public System.Guid DeviceId { get; init; }

        [JsonPropertyName("deviceSerialNumber")]
        [System.ComponentModel.DataAnnotations.Required(AllowEmptyStrings = true)]
        public string DeviceSerialNumber { get; init; }

        [JsonPropertyName("deviceModel")]
        [System.ComponentModel.DataAnnotations.Required(AllowEmptyStrings = true)]
        public string DeviceModel { get; init; }

        [JsonPropertyName("clientId")]
        [System.ComponentModel.DataAnnotations.Required(AllowEmptyStrings = true)]
        public System.Guid ClientId { get; init; }

        [JsonPropertyName("clientName")]
        [System.ComponentModel.DataAnnotations.Required(AllowEmptyStrings = true)]
        public string ClientName { get; init; }

        [JsonPropertyName("description")]
        [System.ComponentModel.DataAnnotations.Required(AllowEmptyStrings = true)]
        public string Description { get; init; }

        [JsonPropertyName("priority")]
        [System.ComponentModel.DataAnnotations.Required(AllowEmptyStrings = true)]
public WorkOrderPriority Priority { get; init; }

        [JsonPropertyName("status")]
        [System.ComponentModel.DataAnnotations.Required(AllowEmptyStrings = true)]
public WorkOrderStatus Status { get; init; }

        [JsonPropertyName("technicianId")]
        public System.Guid? TechnicianId { get; init; }

        [JsonPropertyName("technicianEmail")]
        public string? TechnicianEmail { get; init; }

        [JsonPropertyName("technicianName")]
        public string? TechnicianName { get; init; }

        [JsonPropertyName("dueDate")]
        [System.ComponentModel.DataAnnotations.Required(AllowEmptyStrings = true)]
        public System.DateTimeOffset DueDate { get; init; }

        [JsonPropertyName("isOverdue")]
        public bool IsOverdue { get; init; }

        [JsonPropertyName("createdAt")]
        [System.ComponentModel.DataAnnotations.Required(AllowEmptyStrings = true)]
        public System.DateTimeOffset CreatedAt { get; init; }

        [JsonPropertyName("startedAt")]
        public System.DateTimeOffset? StartedAt { get; init; }

        [JsonPropertyName("completedAt")]
        public System.DateTimeOffset? CompletedAt { get; init; }

        [JsonPropertyName("invoicedAt")]
        public System.DateTimeOffset? InvoicedAt { get; init; }

        private IDictionary<string, object>? _additionalProperties;

        [JsonExtensionData]
        public IDictionary<string, object> AdditionalProperties
        {
            get { return _additionalProperties ?? (_additionalProperties = new Dictionary<string, object>()); }
            set { _additionalProperties = value; }
        }

    }

    [System.CodeDom.Compiler.GeneratedCode("NJsonSchema", "14.7.1.0 (NJsonSchema v11.6.1.0 (Newtonsoft.Json v13.0.0.0))")]
    [JsonConverter(typeof(JsonStringEnumConverter<WorkOrderStatus>))]
    public enum WorkOrderStatus
    {

        [System.Runtime.Serialization.EnumMember(Value = @"New")]
        New = 0,

        [System.Runtime.Serialization.EnumMember(Value = @"Assigned")]
        Assigned = 1,

        [System.Runtime.Serialization.EnumMember(Value = @"InProgress")]
        InProgress = 2,

        [System.Runtime.Serialization.EnumMember(Value = @"Completed")]
        Completed = 3,

        [System.Runtime.Serialization.EnumMember(Value = @"Invoiced")]
        Invoiced = 4,

    }

    [System.CodeDom.Compiler.GeneratedCode("NJsonSchema", "14.7.1.0 (NJsonSchema v11.6.1.0 (Newtonsoft.Json v13.0.0.0))")]
    [JsonConverter(typeof(JsonStringEnumConverter<SortBy>))]
    public enum SortBy
    {

        [System.Runtime.Serialization.EnumMember(Value = @"DueDate")]
        DueDate = 0,

        [System.Runtime.Serialization.EnumMember(Value = @"CreatedAt")]
        CreatedAt = 1,

        [System.Runtime.Serialization.EnumMember(Value = @"Priority")]
        Priority = 2,

        [System.Runtime.Serialization.EnumMember(Value = @"Status")]
        Status = 3,

        [System.Runtime.Serialization.EnumMember(Value = @"ClientName")]
        ClientName = 4,

    }

    [System.CodeDom.Compiler.GeneratedCode("NJsonSchema", "14.7.1.0 (NJsonSchema v11.6.1.0 (Newtonsoft.Json v13.0.0.0))")]
    [JsonConverter(typeof(JsonStringEnumConverter<SortDirection>))]
    public enum SortDirection
    {

        [System.Runtime.Serialization.EnumMember(Value = @"Asc")]
        Asc = 0,

        [System.Runtime.Serialization.EnumMember(Value = @"Desc")]
        Desc = 1,

    }

    [System.CodeDom.Compiler.GeneratedCode("NSwag", "14.7.1.0 (NJsonSchema v11.6.1.0 (Newtonsoft.Json v13.0.0.0))")]
    public partial class FileResponse : System.IDisposable
    {
        private System.IDisposable? _client;
        private System.IDisposable? _response;

        public int StatusCode { get; private set; }

        public IReadOnlyDictionary<string, IEnumerable<string>> Headers { get; private set; }

        public System.IO.Stream Stream { get; private set; }

        public bool IsPartial
        {
            get { return StatusCode == 206; }
        }

        public FileResponse(int statusCode, IReadOnlyDictionary<string, IEnumerable<string>> headers, System.IO.Stream stream, System.IDisposable? client, System.IDisposable? response)
        {
            StatusCode = statusCode;
            Headers = headers;
            Stream = stream;
            _client = client;
            _response = response;
        }

        public void Dispose()
        {
            Stream.Dispose();
            if (_response != null)
                _response.Dispose();
            if (_client != null)
                _client.Dispose();
        }
    }



}

#pragma warning restore  108
#pragma warning restore  114
#pragma warning restore  472
#pragma warning restore  612
#pragma warning restore  649
#pragma warning restore 1573
#pragma warning restore 1591
#pragma warning restore 8073
#pragma warning restore 3016
#pragma warning restore 8600
#pragma warning restore 8602
#pragma warning restore 8603
#pragma warning restore 8604
#pragma warning restore 8625
#pragma warning restore 8765
