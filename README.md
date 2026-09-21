# Digital Library Management System API (.NET Microservices)

## Reflection Notes

### Issues + Resolutions

- Got 502 errors because environment variables were default values which broke the database connection
- Multi-word statuses came out formatted wrong for the API (like "CHECKEDOUT" instead of "CHECKED_OUT") so I wrote a small helper to convert them correctly
- There was no way to create a librarian account anywhere in the app, so checkout and return couldn't be tested at all so I added a small script that creates one automatically during local development
- The book catalog had no way to add books through the API so testing reservations needed sample data so I added a script to seed a few books automatically

### What went well

- Automated tests caught a real bug in status formatting
- Health check endpoints made verifying the deployment faster which was nice
- Once I got past the 502 errors with the bad env variables the communication between all three services worked correctly

## Business Context

### Overview

The Digital Library Management System is a modern microservices-based backend API solution designed to digitize and
streamline library operations for public and institutional libraries transitioning from manual record-keeping to digital
platforms.

### Business Problem

Traditional libraries face several operational challenges:

- Manual tracking of book availability and reservations leads to errors and inefficiency
- Limited visibility into borrowing patterns and inventory usage
- Poor user experience with no self-service capabilities for browsing or reserving books
- Difficulty managing overdue books and calculating late fees
- Time-consuming checkout and return processes at the library desk

### Solution

Our Digital Library Management System provides:

- **Self-service portal** for users to browse, search, and reserve books online
- **Automated reservation management** with 7-day pickup windows
- **Real-time availability tracking** to reduce operational overhead
- **Librarian tools** for efficient checkout and return processing
- **Borrowing history** for patrons to track their reading activity
- **Microservices architecture** for scalability and independent service deployment
- **Cloud-ready infrastructure** for AWS deployment

### Target Users

1. **Library Patrons**: Browse catalog, reserve books, view borrowing history
2. **Librarians**: Process checkouts and returns, manage reservations

---

## Architecture Overview

### Microservices Design

The system consists of **three independent microservices**:

1. **User Service** (Port 5001)
    - User registration and authentication
    - JWT token generation and validation
    - User profile management
    - User validation for other services

2. **Catalog Service** (Port 5002)
    - Book inventory management
    - Catalog browsing and search
    - Book availability tracking
    - Availability updates from reservations

3. **Reservation Service** (Port 5003)
    - Reservation lifecycle management
    - Checkout and return processing
    - Borrowing history
    - Orchestrates calls to User and Catalog services

### Service Communication

- Each service has its own database (Database per Service pattern)
- Services communicate via HTTP/REST APIs
- User Service provides authentication for all services
- Reservation Service orchestrates business workflows

---

## Getting Started

### Core Requirements Documents

**Review these foundational documents before implementation:**

1. **[User Stories](docs/user-stories.md)** - **START HERE**
    - 11 user stories defining all system functionality
    - Business requirements and acceptance criteria
    - Your primary requirements document

2. **[API Contracts](docs/api-contracts.md)** - **CRITICAL**
    - Complete external API interface specification
    - All 10 endpoint definitions with request/response formats
    - Defines the contract you must fulfill

3. **[Development Environment Setup](docs/dev-enviroment-setup.md)**
    - Initial project setup and local development configuration

### Implementation Approach

**Prioritize understanding requirements over implementation details:**

- User Stories define business requirements and desired outcomes
- API Contracts define the exact external interface
- Milestone documents provide technical guidance and acceptance criteria

You have flexibility in **HOW** you implement the solution, but must meet the requirements defined in User Stories and
API Contracts.

---

## Project Structure

### Requirements Documentation

- **[User Stories](docs/user-stories.md)** - Business requirements
- **[API Contracts](docs/api-contracts.md)** - External API interface

### Implementation Guides (Milestones)

1. [Milestone 1: Microservices Architecture & Data Modeling](docs/milestone-1-microservices-architecture-and-data-modeling.md)
2. [Milestone 2: User Service & Authentication](docs/milestone-2-user-service-authentication.md)
3. [Milestone 3: Catalog Service](docs/milestone-3-catalog-service.md)
4. [Milestone 4: Reservation Service](docs/milestone-4-reservation-service-core-functionality.md)
5. [Milestone 5: Deployment & Production Readiness](docs/milestone-5-deployment-production-readiness.md)

### Environment Setup

- [Development Environment Setup](docs/dev-enviroment-setup.md)
- [Production Environment Setup (AWS)](docs/production-enviroment-setup.md)

---

## API Endpoints by Service

### User Service (Port 5001) - 3 Endpoints

- `POST /api/auth/register` - Create new user account
- `POST /api/auth/login` - Authenticate and receive JWT token
- `GET /api/users/profile` - View user profile with statistics

### Catalog Service (Port 5002) - 2 Endpoints

- `GET /api/catalog/books` - Browse and search books with pagination
- `GET /api/catalog/books/{bookId}` - View detailed book information

### Reservation Service (Port 5003) - 5 Endpoints

- `POST /api/reservations` - Reserve an available book
- `GET /api/reservations` - View active reservations
- `POST /api/reservations/{reservationId}/checkout` - Checkout book (Librarian only)
- `POST /api/reservations/{reservationId}/return` - Return book with late fee calculation (Librarian only)
- `GET /api/reservations/history` - View complete borrowing history

**See [API Contracts](docs/api-contracts.md) for complete specifications.**

---

## Technical Stack

### Required Technologies

- **ASP.NET Core**: 8.0 or 9.0
- **C#**: 12
- **Entity Framework Core**: 8.0+
- **PostgreSQL**: 15+ (in-memory for development, RDS for production)
- **.NET CLI** or **Visual Studio 2022**

### Authentication & Security

- **Microsoft.AspNetCore.Authentication.JwtBearer**: JWT token validation
- **System.IdentityModel.Tokens.Jwt**: JWT token generation
- **BCrypt.Net-Next**: Password hashing

### Additional Libraries

Choose appropriate libraries for:

- API documentation (Swashbuckle/Swagger)
- Testing frameworks (xUnit, NUnit, MSTest)
- HTTP client for inter-service communication
- Validation

### Deployment

- **AWS Elastic Beanstalk**: Application hosting (3 separate environments)
- **AWS RDS**: PostgreSQL databases (3 databases)
- **VPC & Security Groups**: Network security and service communication

---

## Success Criteria

> Capstones are graded Pass/Fail with a score out of 20 and instructor feedback.

### Microservices Architecture

- Three independent services deployed and running
- Each service has its own database
- Inter-service communication working correctly
- Services can be deployed and scaled independently

### User Story Compliance

- All 11 user stories fully implemented
- All acceptance criteria met
- All business rules enforced (5 reservation limit, 7-day expiry, 14-day checkout, $1/day late fees)

### API Contract Compliance

- All 10 endpoints implemented as specified across all services
- Request/response formats match exactly
- HTTP status codes correct
- Error response format consistent
- Authentication and authorization working properly

### Technical Quality

- Minimum 80% test coverage across all services
- All endpoints tested (unit and integration)
- Inter-service communication tested
- Proper error handling (400, 401, 403, 404, 500)
- Security properly implemented (JWT, role-based access)
- Successfully deployed to cloud environment

### Functional Verification

- Complete reservation lifecycle works across services (reserve → checkout → return)
- Role-based access control enforced (Patron vs Librarian)
- Real-time availability tracking works correctly
- Late fee calculation accurate
- User profile retrieves statistics from Reservation Service
- Reservation Service validates users via User Service
- Reservation Service updates availability via Catalog Service

---

## Development Philosophy

### Requirements-Driven Development

1. Understand the requirements (User Stories and API Contracts)
2. Design your microservices architecture (service boundaries, communication)
3. Plan your implementation (data models, inter-service contracts)
4. Build to meet the contract
5. Verify completeness (test against acceptance criteria)

### Implementation Flexibility

You decide:

- Internal code organization and architecture for each service
- Service layer design patterns
- Repository implementation approaches
- Validation strategies
- Testing frameworks
- Error handling mechanisms
- HTTP client implementation for inter-service calls

### Non-Negotiable Constraints

You must adhere to:

- User Story requirements and acceptance criteria
- API Contract specifications
- Microservices architecture (3 independent services)
- Business rules (reservation limits, dates, fees)
- Technology stack (ASP.NET Core, PostgreSQL, JWT)
- Security requirements (authentication, authorization)
- Database per Service pattern

---

## Quick Start Guide

1. Read [User Stories](docs/user-stories.md) to understand what you're building
2. Study [API Contracts](docs/api-contracts.md) to understand the exact API interface
3. Review [Milestone 1](docs/milestone-1-microservices-architecture-data-modeling.md) for microservices architecture
4. Set up your environment using [Development Environment Setup](docs/dev-environment-setup.md)
5. Build each service following Milestones 2-4
6. Test comprehensively across all services (Milestone 5)
7. Deploy to production following Milestone 6 guidance

---

## Local Development

### Running All Services

Each service runs on a different port:

```bash
# Terminal 1 - User Service
cd UserService
dotnet run
# Runs on http://localhost:5001

# Terminal 2 - Catalog Service
cd CatalogService
dotnet run
# Runs on http://localhost:5002

# Terminal 3 - Reservation Service
cd ReservationService
dotnet run
# Runs on http://localhost:5003
```

### Accessing Swagger UI

- User Service: http://localhost:5001/swagger
- Catalog Service: http://localhost:5002/swagger
- Reservation Service: http://localhost:5003/swagger

---

## Support & Resources

- **User Stories**: Business requirements and functionality definitions
- **API Contracts**: External API interface specifications
- **Milestone Guides**: Implementation guidance and acceptance criteria
- **ASP.NET Core Documentation**: Framework reference
- **Entity Framework Core Documentation**: ORM reference
- **PostgreSQL Documentation**: Database reference

---

**Remember**: User Stories and API Contracts define **WHAT** you must build. Milestone documents suggest **HOW** you
might approach it, but you have flexibility in implementation as long as you meet the requirements and follow the
microservices architecture.
