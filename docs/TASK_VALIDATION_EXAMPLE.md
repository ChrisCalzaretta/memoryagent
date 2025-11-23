# Task Validation - Practical Examples

## Quick Start Example

Let's say you're building a new feature: **User Profile Management**

### Step 1: Create a Plan with Validation Rules

```json
POST http://localhost:5098/api/plan/add

{
  "context": "cbc_ai",
  "name": "User Profile Management",
  "description": "Allow users to view and edit their profiles",
  "tasks": [
    {
      "title": "Create ProfileService",
      "description": "Core service for profile operations",
      "orderIndex": 0,
      "dependencies": [],
      "validationRules": [
        {
          "ruleType": "requires_test",
          "target": "ProfileService",
          "autoFix": true
        },
        {
          "ruleType": "max_complexity",
          "target": "ProfileService",
          "parameters": {
            "max_complexity": 10
          },
          "autoFix": false
        },
        {
          "ruleType": "requires_documentation",
          "target": "ProfileService",
          "autoFix": true
        }
      ]
    },
    {
      "title": "Add Profile API Endpoints",
      "description": "REST endpoints for profile CRUD",
      "orderIndex": 1,
      "dependencies": ["task-0"],
      "validationRules": [
        {
          "ruleType": "requires_test",
          "target": "ProfileController",
          "autoFix": true
        },
        {
          "ruleType": "min_test_coverage",
          "target": "ProfileController",
          "parameters": {
            "min_coverage": 80
          },
          "autoFix": true
        }
      ]
    },
    {
      "title": "Add Profile UI",
      "description": "Frontend components for profile",
      "orderIndex": 2,
      "dependencies": ["task-1"],
      "validationRules": [
        {
          "ruleType": "requires_file",
          "target": "src/components/Profile.tsx",
          "autoFix": false
        }
      ]
    }
  ]
}
```

**Response:**
```json
{
  "id": "plan-abc-123",
  "context": "cbc_ai",
  "name": "User Profile Management",
  "status": "Active",
  "tasks": [
    {
      "id": "task-0",
      "title": "Create ProfileService",
      "status": "Pending",
      "validationRules": [...]
    },
    {
      "id": "task-1",
      "title": "Add Profile API Endpoints",
      "status": "Pending",
      "validationRules": [...]
    },
    {
      "id": "task-2",
      "title": "Add Profile UI",
      "status": "Pending",
      "validationRules": [...]
    }
  ]
}
```

### Step 2: Work on Task 1 - Create ProfileService

You write the code:

```csharp
// ProfileService.cs
public class ProfileService
{
    private readonly IDbContext _db;

    public async Task<UserProfile> GetProfileAsync(string userId)
    {
        return await _db.Profiles.FindAsync(userId);
    }

    public async Task<bool> UpdateProfileAsync(UserProfile profile)
    {
        _db.Profiles.Update(profile);
        return await _db.SaveChangesAsync() > 0;
    }
}
```

### Step 3: Try to Mark Task Complete

**From Cursor:**
```javascript
// Call the validate_task tool first
{
  "planId": "plan-abc-123",
  "taskId": "task-0",
  "autoFix": true
}
```

**Result:**
```
❌ Task 'Create ProfileService' failed validation:

• requires_test: No tests found for ProfileService
  💡 Auto-fix available: Create ProfileServiceTests.cs with basic test scaffolding

• requires_documentation: 2 public method(s) lack documentation
  💡 Auto-fix available: Generate XML documentation for 2 method(s)

🔧 Attempting auto-fix...

✅ Created: ProfileServiceTests.cs
✅ Added XML doc templates to ProfileService.cs

✅ Auto-fix completed! Please re-validate to confirm.
```

### Step 4: System Auto-Created Test File

```csharp
// ProfileServiceTests.cs (auto-generated)
using Xunit;

namespace ProfileServiceTests
{
    public class ProfileServiceTests
    {
        [Fact]
        public void ProfileService_ShouldWork()
        {
            // Arrange
            var sut = new ProfileService();

            // Act
            // TODO: Add test logic

            // Assert
            Assert.NotNull(sut);
        }
    }
}
```

### Step 5: You Complete the Tests

```csharp
// ProfileServiceTests.cs (your implementation)
using Xunit;
using Moq;

namespace ProfileServiceTests
{
    public class ProfileServiceTests
    {
        [Fact]
        public async Task GetProfileAsync_ReturnsProfile_WhenUserExists()
        {
            // Arrange
            var mockDb = new Mock<IDbContext>();
            mockDb.Setup(x => x.Profiles.FindAsync("user1"))
                  .ReturnsAsync(new UserProfile { UserId = "user1", Name = "John" });
            
            var sut = new ProfileService(mockDb.Object);

            // Act
            var result = await sut.GetProfileAsync("user1");

            // Assert
            Assert.NotNull(result);
            Assert.Equal("John", result.Name);
        }

        [Fact]
        public async Task UpdateProfileAsync_ReturnsTrue_WhenSaveSucceeds()
        {
            // Arrange
            var mockDb = new Mock<IDbContext>();
            mockDb.Setup(x => x.SaveChangesAsync()).ReturnsAsync(1);
            
            var sut = new ProfileService(mockDb.Object);
            var profile = new UserProfile { UserId = "user1", Name = "Jane" };

            // Act
            var result = await sut.UpdateProfileAsync(profile);

            // Assert
            Assert.True(result);
        }
    }
}
```

### Step 6: Fill in Documentation

```csharp
// ProfileService.cs (with docs)
public class ProfileService
{
    private readonly IDbContext _db;

    /// <summary>
    /// Retrieves a user profile by user ID
    /// </summary>
    /// <param name="userId">The unique identifier of the user</param>
    /// <returns>The user's profile, or null if not found</returns>
    public async Task<UserProfile> GetProfileAsync(string userId)
    {
        return await _db.Profiles.FindAsync(userId);
    }

    /// <summary>
    /// Updates an existing user profile
    /// </summary>
    /// <param name="profile">The profile data to update</param>
    /// <returns>True if update was successful, false otherwise</returns>
    public async Task<bool> UpdateProfileAsync(UserProfile profile)
    {
        _db.Profiles.Update(profile);
        return await _db.SaveChangesAsync() > 0;
    }
}
```

### Step 7: Re-Validate

**From Cursor:**
```javascript
{
  "planId": "plan-abc-123",
  "taskId": "task-0",
  "autoFix": false
}
```

**Result:**
```
✅ Task 'Create ProfileService' passed all validation rules!

Task is ready to be marked as completed.
```

### Step 8: Mark Task Complete

```json
POST http://localhost:5098/api/plan/task/status

{
  "planId": "plan-abc-123",
  "taskId": "task-0",
  "status": "Completed"
}
```

**Response:**
```json
{
  "id": "plan-abc-123",
  "tasks": [
    {
      "id": "task-0",
      "title": "Create ProfileService",
      "status": "Completed",  // ✅ Success!
      "completedAt": "2025-11-22T14:30:00Z"
    },
    {
      "id": "task-1",
      "status": "Pending",
      "dependencies": ["task-0"]  // Can now start this task
    }
  ]
}
```

## Example 2: Complexity Refactoring

### Scenario: Method Too Complex

You have a method with complexity 15 (limit is 10):

```csharp
// OrderService.cs
public class OrderService
{
    /// <summary>
    /// Process an order with validation and notifications
    /// </summary>
    public async Task<bool> ProcessOrderAsync(Order order)
    {
        // Complexity: 15 ❌
        
        if (order == null) return false;
        
        if (order.Items.Count == 0) return false;
        
        if (order.Total <= 0) return false;
        
        if (string.IsNullOrEmpty(order.CustomerId)) return false;
        
        var customer = await _db.Customers.FindAsync(order.CustomerId);
        if (customer == null) return false;
        
        if (customer.IsBlocked) return false;
        
        if (customer.CreditLimit < order.Total) return false;
        
        foreach (var item in order.Items)
        {
            var product = await _db.Products.FindAsync(item.ProductId);
            if (product == null) return false;
            
            if (product.Stock < item.Quantity)
            {
                await SendStockAlertAsync(product);
                return false;
            }
            
            product.Stock -= item.Quantity;
        }
        
        _db.Orders.Add(order);
        await _db.SaveChangesAsync();
        
        await SendOrderConfirmationAsync(order);
        
        return true;
    }
}
```

### Task with Complexity Rule

```json
{
  "title": "Refactor ProcessOrder",
  "validationRules": [
    {
      "ruleType": "max_complexity",
      "target": "OrderService.ProcessOrderAsync",
      "parameters": {
        "max_complexity": 10
      },
      "autoFix": false
    }
  ]
}
```

### Validation Fails

```
❌ Task 'Refactor ProcessOrder' failed validation:

• max_complexity: Method ProcessOrderAsync has complexity 15 (limit: 10)
  💡 Refactor: ProcessOrderAsync

This requires manual refactoring (no auto-fix available)
```

### You Refactor

```csharp
// OrderService.cs (refactored)
public class OrderService
{
    /// <summary>
    /// Process an order with validation and notifications
    /// </summary>
    public async Task<bool> ProcessOrderAsync(Order order)
    {
        // Complexity: 4 ✅
        
        if (!ValidateOrder(order, out var validationError))
        {
            _logger.LogWarning("Order validation failed: {Error}", validationError);
            return false;
        }
        
        var customer = await GetValidatedCustomerAsync(order.CustomerId);
        if (customer == null) return false;
        
        if (!await ProcessOrderItemsAsync(order.Items))
        {
            return false;
        }
        
        await SaveOrderAsync(order);
        await SendOrderConfirmationAsync(order);
        
        return true;
    }

    /// <summary>
    /// Validate order basic properties
    /// </summary>
    private bool ValidateOrder(Order order, out string error)
    {
        // Complexity: 3
        if (order == null)
        {
            error = "Order is null";
            return false;
        }
        
        if (order.Items.Count == 0)
        {
            error = "No items";
            return false;
        }
        
        if (order.Total <= 0)
        {
            error = "Invalid total";
            return false;
        }
        
        error = string.Empty;
        return true;
    }

    /// <summary>
    /// Get customer and validate status
    /// </summary>
    private async Task<Customer?> GetValidatedCustomerAsync(string customerId)
    {
        // Complexity: 2
        var customer = await _db.Customers.FindAsync(customerId);
        
        if (customer == null || customer.IsBlocked)
        {
            return null;
        }
        
        return customer;
    }

    /// <summary>
    /// Process all items and update stock
    /// </summary>
    private async Task<bool> ProcessOrderItemsAsync(List<OrderItem> items)
    {
        // Complexity: 3
        foreach (var item in items)
        {
            var product = await _db.Products.FindAsync(item.ProductId);
            
            if (product == null || product.Stock < item.Quantity)
            {
                if (product != null)
                {
                    await SendStockAlertAsync(product);
                }
                return false;
            }
            
            product.Stock -= item.Quantity;
        }
        
        return true;
    }

    private async Task SaveOrderAsync(Order order)
    {
        _db.Orders.Add(order);
        await _db.SaveChangesAsync();
    }
}
```

### Validation Passes

```
✅ Task 'Refactor ProcessOrder' passed all validation rules!

Methods:
  - ProcessOrderAsync: Complexity 4 ✅
  - ValidateOrder: Complexity 3 ✅
  - GetValidatedCustomerAsync: Complexity 2 ✅
  - ProcessOrderItemsAsync: Complexity 3 ✅

All methods are under the limit of 10!
```

## Example 3: Cursor Workflow

### In Cursor Chat

**You:** "I finished implementing the PaymentService"

**Cursor (using validate_task tool):**
```
Let me check if it's ready to mark complete...

❌ Validation failed:
  - No tests found for PaymentService
  - Missing XML documentation for 3 public methods

I can auto-fix these. Should I create the test scaffolding and doc templates?
```

**You:** "Yes please"

**Cursor (calls validate_task with autoFix: true):**
```
✅ Created PaymentServiceTests.cs
✅ Added documentation templates

Here's what I generated:

[Shows test file]

Now you just need to fill in the actual test logic. The structure is all there.
```

**You:** *Fills in tests*

**Cursor (auto-validates again):**
```
✅ All validation rules passed!

Marking task as complete...
✅ Task completed successfully!

Next task is now available: "Add Payment UI"
```

## Example 4: Complex Multi-Rule Scenario

### The Plan

```json
{
  "title": "Implement Critical Payment Feature",
  "validationRules": [
    {
      "ruleType": "requires_test",
      "target": "PaymentService",
      "autoFix": true
    },
    {
      "ruleType": "min_test_coverage",
      "target": "PaymentService",
      "parameters": {
        "min_coverage": 90
      },
      "autoFix": true
    },
    {
      "ruleType": "max_complexity",
      "target": "PaymentService",
      "parameters": {
        "max_complexity": 8
      },
      "autoFix": false
    },
    {
      "ruleType": "requires_documentation",
      "target": "PaymentService",
      "autoFix": true
    },
    {
      "ruleType": "no_code_smells",
      "target": "PaymentService",
      "autoFix": false
    }
  ]
}
```

### Validation Run 1

```
❌ Task validation failed:

✓ requires_test: PASSED (tests found)
❌ min_test_coverage: FAILED (coverage 40%, needs 90%)
   💡 Need 5 more tests (you have 4 of 10 methods tested)
❌ max_complexity: FAILED (ProcessPayment has complexity 12, limit 8)
   💡 Refactor: ProcessPayment, ValidateCard
✓ requires_documentation: PASSED
❌ no_code_smells: FAILED (2 code smells)
   💡 Issues:
     - ProcessPayment: Too many parameters (8)
     - ValidateCard: Deep nesting (level 5)

🔧 Auto-fix can help with:
  - min_test_coverage: Generate 5 additional test stubs

Manual fixes needed:
  - Reduce complexity in ProcessPayment
  - Fix code smells (too many params, deep nesting)
```

### After Refactoring

```
✅ Task 'Implement Critical Payment Feature' passed all validation rules!

Summary:
  ✅ requires_test: PaymentServiceTests.cs exists
  ✅ min_test_coverage: 90% (9/10 methods tested)
  ✅ max_complexity: All methods < 8
  ✅ requires_documentation: All public methods documented
  ✅ no_code_smells: No issues detected

Ready to mark as complete!
```

## Tips for Using Validation

### 1. Start with Auto-Fix Enabled

Let the system help you:
```json
{
  "ruleType": "requires_test",
  "autoFix": true  // ✅ Yes!
}
```

### 2. Use Realistic Thresholds

```json
{
  "ruleType": "max_complexity",
  "parameters": {
    "max_complexity": 10  // ✅ Reasonable
    // NOT: "max_complexity": 3  // ❌ Too strict
  }
}
```

### 3. Validate Before Completing

Always run `validate_task` before trying to mark complete:
```
1. Write code
2. Validate (auto-fix if needed)
3. Fix remaining issues
4. Re-validate
5. Mark complete ✅
```

### 4. Use Dependencies

```json
{
  "tasks": [
    {
      "title": "Write Code",
      "validationRules": []  // No rules yet
    },
    {
      "title": "Add Tests",
      "dependencies": ["task-0"],  // Must finish code first
      "validationRules": [
        { "ruleType": "min_test_coverage", "parameters": { "min_coverage": 80 } }
      ]
    }
  ]
}
```

## Conclusion

The Task Validation System ensures your development tasks are **actually done**, not just checked off. It:

✅ **Enforces quality** (tests, docs, complexity)
✅ **Saves time** (auto-generates boilerplate)
✅ **Catches issues early** (before marking complete)
✅ **Improves code** (objective quality metrics)

**Ready to try it?** Create your first plan with validation rules! 🚀

