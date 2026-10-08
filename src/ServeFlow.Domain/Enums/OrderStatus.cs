namespace ServeFlow.Domain.Enums;

public enum OrderStatus
{
    Pending = 0,
    Accepted = 1,
    Preparing = 2,
    Ready = 3,
    Served = 4,
    Completed = 5,
    Cancelled = 6
}
