# Milestone 1: Microservices Architecture & Data Modeling

**Goal:** Design microservices architecture and create entity classes for the Library Management System

**Related User Stories:** Foundation for US-001 through US-014

---

## Business Requirements

The Library Management System must be built as a **microservices architecture** with three independent services:

### Service Boundaries

**1. User Service** (Authentication & User Management)
- Handles user registration and authentication
- Manages user profiles and membership status
- Issues and validates JWT tokens
- Provides user data to other services

**2. Catalog Service** (Book Inventory Management)
- Manages book catalog and inventory
- Handles book search and filtering
- Tracks total copies and available copies
- Provides book availability information

**3. Reservation Service** (Borrowing & Returns)
- Manages reservation lifecycle (reserve, checkout, return)
- Tracks borrowing history
- Calculates late fees
- Communicates with User Service for authentication
- Communicates with Catalog Service for book information and availability updates

### Key Business Rules

**Users:**
- Users have roles (Patron or Librarian) that determine system access
- Users can have membership statuses (Active or Suspended)
- Users track membership start date (MemberSince)

**Books:**
- Books track total copies and available copies for inventory management
- Book availability status is derived from available copies (not stored separately)
- Each book has unique ISBN

**Reservations:**
- Reservations track the complete lifecycle: Reserved → CheckedOut → Returned
- Reservations expire after 7 days if not picked up (ExpiresAt)
- Checkout period is 14 days (DueDate)
- Late fees calculated at $1.00 per day
- Maximum 5 active reservations per user
- Book condition recorded upon return

**Waitlist:**
- Patrons may join a waitlist when a book has no available copies (availableCopies = 0)
- When a copy is returned and a waitlist exists for that book, the copy is offered to the longest-waiting
  eligible patron instead of becoming generally available
- "Eligible" means the patron is still under their 5-active-reservation limit at the moment their turn comes
  up - a patron over the limit is skipped (their waitlist entry expires) and the copy cascades to the next
  person in line
- A notified patron has 48 hours to claim their held copy (a normal Reservation is auto-created for them)
  before it cascades to the next person in the queue
- If the queue is empty (or everyone in it is skipped for being over the limit), the copy is released back
  to general availability, same as today's behavior

---

## General Technical Requirements

**Architecture:**
- Three independent ASP.NET Core applications (microservices)
- Each service runs on a different port
- Each service has its own database (Database per Service pattern)
- Services communicate via HTTP/REST APIs
- Stateless authentication using JWT tokens

**Technology Stack:**
- ASP.NET Core 8.0 or 9.0
- C# 12
- Entity Framework Core 8.0+
- PostgreSQL 15+ (or in-memory for development)
- RESTful API communication between services

**Service Communication:**
- User Service validates JWT tokens for other services
- Reservation Service calls Catalog Service to check/update book availability
- Reservation Service calls User Service to validate users and check reservation limits
- All inter-service calls use HTTP clients

**Data Integrity:**
- Each service owns its data
- No direct database access between services
- Email addresses must be unique (User Service)
- ISBN must be unique (Catalog Service)
- Passwords must be stored securely hashed

---

## Deliverables

### 1. Service Architecture Design

Define three separate ASP.NET Core projects:

**UserService (Port: 5001)**
- Handles: Registration, Login, Profile, User validation
- Database: UserServiceDb
- Exposes endpoints:
  - POST /api/auth/register
  - POST /api/auth/login
  - GET /api/users/profile
  - GET /api/users/{userId}/validate (internal - for other services)

**CatalogService (Port: 5002)**
- Handles: Book browsing, search, availability management
- Database: CatalogServiceDb
- Exposes endpoints:
  - GET /api/catalog/books
  - GET /api/catalog/books/{bookId}
  - PUT /api/catalog/books/{bookId}/availability (internal - for Reservation Service)

**ReservationService (Port: 5003)**
- Handles: Reservations, checkout, returns, history, waitlist
- Database: ReservationServiceDb
- Exposes endpoints:
  - POST /api/reservations
  - GET /api/reservations
  - POST /api/reservations/{reservationId}/checkout
  - POST /api/reservations/{reservationId}/return
  - GET /api/reservations/history
  - POST /api/reservations/waitlist
  - GET /api/reservations/waitlist
  - DELETE /api/reservations/waitlist/{waitlistId}
- Also runs: a background job (see Deliverable 6) that periodically expires stale waitlist claims

### 2. Entity Classes Per Service

**User Service Entities:**

**User Entity:**
- UserId (Guid, Primary Key)
- Email (string, unique, required, max 255)
- PasswordHash (string, required)
- FirstName (string, required, max 100)
- LastName (string, required, max 100)
- PhoneNumber (string, required, max 20)
- Role (enum: Patron, Librarian)
- MembershipStatus (enum: Active, Suspended)
- MemberSince (DateTime?, nullable)
- CreatedAt, UpdatedAt (audit fields)

**Catalog Service Entities:**

**Book Entity:**
- BookId (Guid, Primary Key)
- Isbn (string, unique, required, max 20)
- Title (string, required, max 255)
- Author (string, required, max 255)
- Genre (string, required, max 100)
- PublicationYear (int?, nullable)
- Description (string, nullable)
- Publisher (string, nullable, max 255)
- PageCount (int?, nullable)
- Language (string, nullable, max 50)
- TotalCopies (int, required, default: 0)
- AvailableCopies (int, required, default: 0)
- CreatedAt, UpdatedAt (audit fields)

**Reservation Service Entities:**

**Reservation Entity:**
- ReservationId (Guid, Primary Key)
- BookId (Guid, reference to Catalog Service)
- UserId (Guid, reference to User Service)
- Status (enum: Reserved, CheckedOut, Returned, Cancelled)
- ReservedAt (DateTime, required)
- ExpiresAt (DateTime?, nullable)
- CheckedOutAt (DateTime?, nullable)
- DueDate (DateTime?, nullable)
- ReturnedAt (DateTime?, nullable)
- RenewalCount (int, default: 0)
- LateDays (int?, nullable)
- LateFee (decimal?, nullable)
- Condition (enum: Good, Fair, Poor, Damaged, nullable)
- Notes (string, nullable)
- BookTitle (string, cached from Catalog Service)
- BookAuthor (string, cached from Catalog Service)
- CreatedAt, UpdatedAt (audit fields)

**Waitlist Entity:**
- WaitlistId (Guid, Primary Key)
- BookId (Guid, reference to Catalog Service)
- UserId (Guid, reference to User Service)
- Status (enum: Waiting, Notified, Claimed, Expired, Cancelled)
- JoinedAt (DateTime, required)
- NotifiedAt (DateTime?, nullable) - set when a held copy is offered to this entry
- ClaimDeadline (DateTime?, nullable) - NotifiedAt + 48 hours; set alongside NotifiedAt
- ResultingReservationId (Guid?, nullable) - set to the auto-created Reservation's Id once claimed
- BookTitle (string, cached from Catalog Service)
- BookAuthor (string, cached from Catalog Service)
- CreatedAt, UpdatedAt (audit fields)

### 3. Enum Types

**User Service Enums:**
```csharp
public enum Role { Patron, Librarian }
public enum MembershipStatus { Active, Suspended }
```

**Reservation Service Enums:**
```csharp
public enum ReservationStatus { Reserved, CheckedOut, Returned, Cancelled }
public enum BookCondition { Good, Fair, Poor, Damaged }
public enum WaitlistStatus { Waiting, Notified, Claimed, Expired, Cancelled }
```

### 4. DbContext Per Service

Create separate DbContext for each service. Each service reads its own connection string key (`UserDb`,
`CatalogDb`, `ReservationDb`), not a shared `DefaultConnection`, and calls `Database.Migrate()` at startup so the
database and tables are created on first run.
- **UserServiceContext** - manages User entity
- **CatalogServiceContext** - manages Book entity
- **ReservationServiceContext** - manages Reservation entity

### 5. Inter-Service Communication Setup

**HTTP Client Configuration:**
- User Service validates tokens and provides user data
- Catalog Service provides book data and manages availability
- Reservation Service orchestrates workflow by calling other services

**Example Communication Flows:**

*Creating a Reservation:*
1. Reservation Service receives request with JWT token
2. Calls User Service to validate token and check user's active reservation count
3. Calls Catalog Service to verify book availability
4. Calls Catalog Service to decrement available copies
5. Creates reservation in Reservation Service database

*Returning a Book:*
1. Reservation Service receives return request
2. Updates reservation status to Returned
3. Calls Catalog Service to increment available copies

---

## Acceptance Criteria

- [ ] Three separate ASP.NET Core projects created (UserService, CatalogService, ReservationService)
- [ ] Each service runs independently on different ports (5001, 5002, 5003)
- [ ] Each service has its own DbContext and database
- [ ] All entity classes created with proper data annotations, including Waitlist
- [ ] All enum types defined in appropriate services, including WaitlistStatus
- [ ] Entity relationships properly configured within each service
- [ ] Audit fields (CreatedAt, UpdatedAt) auto-populate in each service
- [ ] HTTP client configured for inter-service communication
- [ ] All three services can start and run simultaneously
- [ ] Each service can be tested independently via Swagger
- [ ] Basic CRUD operations work in each service's database

---

## Suggested Approach

1. Create three separate ASP.NET Core Web API projects
2. Configure each project to run on different ports (appsettings.json)
3. Design entity classes for each service based on service boundaries
4. Create DbContext for each service
5. Define enum types in appropriate services
6. Configure Entity Framework for each service
7. Set up HTTP client infrastructure for inter-service calls
8. Create basic controllers to verify each service works independently
9. Test running all three services simultaneously
10. Verify services can communicate via HTTP

**Note:** You have flexibility in how you structure your projects, organize your code, and implement inter-service communication. Focus on clear service boundaries and loose coupling between services.

---

## Service Ports Configuration

Configure in each service's `appsettings.json`:

**UserService:**
```json
{
  "urls": "http://localhost:5001"
}
```

**CatalogService:**
```json
{
  "urls": "http://localhost:5002"
}
```

**ReservationService:**
```json
{
  "urls": "http://localhost:5003",
  "ServiceUrls": {
    "UserService": "http://localhost:5001",
    "CatalogService": "http://localhost:5002"
  }
}
```

These ports are local defaults. In production the deployment starts each service with `--urls`, which overrides
them, so never hardcode a port in `Program.cs`.

---

## Resources

- Refer to `user-stories.md` for detailed functional requirements
- Refer to `api-contracts.md` for API endpoint specifications
- ASP.NET Core documentation for multiple project solutions
- Entity Framework Core documentation for DbContext configuration
- HttpClient documentation for inter-service communication