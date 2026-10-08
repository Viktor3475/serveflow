using ServeFlow.Application.Operations;
using ServeFlow.Application.Security;
using ServeFlow.Domain.Enums;

namespace ServeFlow.UnitTests;

public sealed class OrderTransitionTests
{
    [Theory]
    [InlineData(OrderStatus.Pending, OrderStatus.Accepted)]
    [InlineData(OrderStatus.Accepted, OrderStatus.Preparing)]
    [InlineData(OrderStatus.Preparing, OrderStatus.Ready)]
    public void KitchenCanAdvancePreparation(OrderStatus from, OrderStatus to) => Assert.Contains(to, OrderTransitions.Allowed(from, [StaffRoles.KitchenStaff]));
    [Theory]
    [InlineData(OrderStatus.Ready, OrderStatus.Served)]
    [InlineData(OrderStatus.Served, OrderStatus.Completed)]
    public void WaiterCanAdvanceService(OrderStatus from, OrderStatus to) => Assert.Contains(to, OrderTransitions.Allowed(from, [StaffRoles.Waiter]));
    [Theory]
    [InlineData(OrderStatus.Pending)] [InlineData(OrderStatus.Accepted)] [InlineData(OrderStatus.Preparing)]
    public void WaiterCannotAdvancePreparation(OrderStatus from) => Assert.DoesNotContain(OrderTransitions.Allowed(from, [StaffRoles.Waiter]), x => x != OrderStatus.Cancelled);
    [Theory]
    [InlineData(OrderStatus.Ready)] [InlineData(OrderStatus.Served)]
    public void KitchenCannotServeOrComplete(OrderStatus from) => Assert.Empty(OrderTransitions.Allowed(from, [StaffRoles.KitchenStaff]));
    [Theory]
    [InlineData(OrderStatus.Completed)] [InlineData(OrderStatus.Cancelled)]
    public void TerminalOrdersHaveNoActionsForAnyStaff(OrderStatus status)
    {
        Assert.True(OrderTransitions.Terminal(status));
        Assert.Empty(OrderTransitions.Allowed(status, StaffRoles.EmployeeRoles));
    }
    [Theory]
    [InlineData(OrderStatus.Pending)] [InlineData(OrderStatus.Accepted)] [InlineData(OrderStatus.Preparing)]
    [InlineData(OrderStatus.Ready)] [InlineData(OrderStatus.Served)]
    public void CustomerNeverHasStaffActions(OrderStatus status) => Assert.Empty(OrderTransitions.Allowed(status, [StaffRoles.Customer]));
    [Fact] public void AdministratorMayCancelReadyButNotServed() { Assert.Contains(OrderStatus.Cancelled, OrderTransitions.Allowed(OrderStatus.Ready, [StaffRoles.Administrator])); Assert.DoesNotContain(OrderStatus.Cancelled, OrderTransitions.Allowed(OrderStatus.Served, [StaffRoles.Administrator])); }
    [Fact] public void StatusSkippingIsNeverOffered() => Assert.DoesNotContain(OrderStatus.Ready, OrderTransitions.Allowed(OrderStatus.Pending, [StaffRoles.Administrator]));
}
