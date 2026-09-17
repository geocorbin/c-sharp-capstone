# Milestone 5: Deployment & Production Readiness

**Goal:** Deploy all three microservices to cloud infrastructure with production databases

**Related User Stories:** All (US-001 through US-014) - Production deployment

---

## Business Requirements

### Deployment Objectives
- All three microservices must be publicly accessible via internet
- System must use production-grade databases (not in-memory)
- All API endpoints must function in production environment
- Applications must be secure and properly configured
- Database credentials and secrets must be protected
- Services must be able to communicate with each other in production

### Production Environment Requirements
- Three publicly accessible API endpoints (one per service, as path prefixes on one hostname)
- Persistent data storage for each service
- Environment-specific configuration
- Secure credential management
- Health monitoring capability
- Inter-service communication properly configured

---

## General Technical Requirements

**Deployment Platform:**
- AWS Elastic Beanstalk (or equivalent cloud platform)
- .NET 10 runtime environment
- Three services deployed as separate processes in one Elastic Beanstalk environment (the sandbox allows two EC2 instances)
- `t3.medium` EC2 instance and `db.t3.micro` RDS instance (the only sizes the sandbox allows)

**Database:**
- PostgreSQL 15.x on AWS RDS (or equivalent)
- Three separate databases (one per service)
- Persistent storage
- Secure network configuration
- Automated backups

**Configuration:**
- Environment-based configuration management
- Secure storage of sensitive data (passwords, secrets, API keys)
- Port configuration for cloud platform
- Database connection parameters
- Service URL configuration for inter-service communication

**Security:**
- Restricted database access (not publicly accessible)
- Network security groups configured correctly
- Secure JWT secret generation and storage
- HTTPS support (recommended)

---

## Deliverables

### 1. Application Build
Prepare each microservice for deployment:
- Build production-ready packages (publish output) for all three services
- Verify builds include all dependencies and migrations
- Ensure configuration supports environment variables
- Create deployment packages (ZIP files) for each service

### 2. Database Setup
Create production databases:
- Three PostgreSQL database instances (or three databases on one instance)
  - UserServiceDb
  - CatalogServiceDb
  - ReservationServiceDb
- Initial database creation
- Secure credential generation
- Network configuration for application access

### 3. Application Deployment
Deploy all three microservices to cloud platform:
- Create three separate application environments
- Upload application artifacts for each service
- Configure runtime environments
- Set up necessary IAM roles and permissions

### 4. Environment Configuration
Configure each service's environment:
- Set appropriate ports or URLs for platform requirements
- Configure database connection parameters for each service
- Set JWT secret (shared across services for token validation)
- Configure service URLs for inter-service communication
- Enable production profile

### 5. Network Security
Configure secure network access:
- Set up security groups
- Allow each application to connect to its database
- Allow services to communicate with each other
- Restrict databases to private network
- Configure public application accessibility

### 6. Verification
Verify deployment success for all services:
- Confirm all application health endpoints respond
- Test all API endpoints across services
- Verify database connectivity for each service
- Check API documentation accessibility for each service
- Test inter-service communication

---

## Required Environment Configuration

### User Service Environment Variables:
- **ASPNETCORE_ENVIRONMENT:** Production
- **ConnectionStrings__UserDb:** PostgreSQL connection string for UserServiceDb
- **Jwt__Secret:** Secure JWT secret (shared across all services)
- **Jwt__Issuer:** Token issuer name
- **Jwt__Audience:** Token audience name
- **ServiceUrls__ReservationService:** URL of Reservation Service (`http://localhost:5003` in production)

### Catalog Service Environment Variables:
- **ASPNETCORE_ENVIRONMENT:** Production
- **ConnectionStrings__CatalogDb:** PostgreSQL connection string for CatalogServiceDb

### Reservation Service Environment Variables:
- **ASPNETCORE_ENVIRONMENT:** Production
- **ConnectionStrings__ReservationDb:** PostgreSQL connection string for ReservationServiceDb
- **Jwt__Secret:** Secure JWT secret (same as User Service)
- **Jwt__Issuer:** Token issuer name (same as User Service)
- **Jwt__Audience:** Token audience name (same as User Service)
- **ServiceUrls__UserService:** URL of User Service (`http://localhost:5000` in production)
- **ServiceUrls__CatalogService:** URL of Catalog Service (`http://localhost:5002` in production)

**Note:** All sensitive values should be configured as environment variables, never hardcoded.

---

## Acceptance Criteria

- [ ] All three microservices build successfully as deployable artifacts
- [ ] Three production PostgreSQL databases created and accessible
- [ ] User Service deployed and running
- [ ] Catalog Service deployed and running
- [ ] Reservation Service deployed and running
- [ ] All environment health shows healthy/running status
- [ ] All environment variables configured correctly for each service
- [ ] Network security allows each application-to-database communication
- [ ] Network security allows inter-service communication
- [ ] Network security restricts public database access
- [ ] Health check endpoints respond successfully for all services
- [ ] API documentation (Swagger) accessible for all services
- [ ] User registration works in production (User Service)
- [ ] User login returns JWT token (User Service)
- [ ] Profile endpoint retrieves statistics from Reservation Service
- [ ] Catalog browsing works without authentication (Catalog Service)
- [ ] Book availability updates work from Reservation Service to Catalog Service
- [ ] Reservation creation validates user via User Service
- [ ] Reservation creation updates availability via Catalog Service
- [ ] Authenticated endpoints require valid token
- [ ] Role-based authorization enforced (LIBRARIAN operations)
- [ ] Database schemas created automatically for all services
- [ ] All 13 API endpoints functional in production across all services
- [ ] Waitlist expiry background job runs and logs its activity in the deployed Reservation Service

---

## Deployment Verification Checklist

After deployment, verify each service and inter-service communication:

### User Service (Port/URL 1)
- Application URL is accessible
- Swagger UI loads
- User can register
- User can login and receive JWT token
- Profile endpoint works (calls Reservation Service for statistics)
- User validation endpoint works (for Reservation Service)

### Catalog Service (Port/URL 2)
- Application URL is accessible
- Swagger UI loads
- Catalog browsing works without authentication
- Book search and filtering work
- Book details retrieval works
- Availability update endpoint works (for Reservation Service)

### Reservation Service (Port/URL 3)
- Application URL is accessible
- Swagger UI loads
- Reservation creation works (validates via User Service, updates via Catalog Service)
- Active reservations display correctly
- Checkout works (LIBRARIAN only)
- Return works (LIBRARIAN only, updates Catalog Service or auto-claims for a waitlisted patron)
- Borrowing history works
- Patron can join, view, and leave a book's waitlist
- Waitlist expiry background job is running (check logs after deployment for its periodic activity, or
  temporarily shorten its interval to verify behavior faster)

### Inter-Service Communication
- User Service successfully calls Reservation Service for profile statistics
- Reservation Service successfully validates users via User Service
- Reservation Service successfully checks book availability via Catalog Service
- Reservation Service successfully updates book availability via Catalog Service

### Authorization
- Patron cannot access checkout endpoint (403)
- Patron cannot access return endpoint (403)
- Librarian can access checkout endpoint
- Librarian can access return endpoint

### Data Persistence
- Created users persist after User Service restart
- Books remain in catalog after Catalog Service restart
- Reservations persist after Reservation Service restart

---

## Troubleshooting Guidelines

If deployment fails or application doesn't work:

**Check Application Health:**
- Review application logs for each service
- Verify all environment variables are set correctly
- Confirm each application started successfully
- Check for port conflicts or binding issues

**Database Connection Issues:**
- Verify database endpoint is correct for each service
- Check database credentials for each service
- Confirm security groups allow connection from each application
- Ensure databases are running and accessible
- Verify database names match configuration

**Inter-Service Communication Issues:**
- Verify service URLs are correctly configured
- Check network security allows service-to-service communication
- Test service endpoints individually
- Review logs for connection errors
- Ensure services can resolve each other's URLs

**Application Errors:**
- Review startup logs for errors in each service
- Verify .NET version compatibility
- Check all required dependencies included
- Confirm migrations are applied successfully

**API Not Working:**
- Verify application started successfully
- Check endpoint mappings in logs
- Test with simple curl commands
- Verify authentication works across services
- Test inter-service calls

---

## Suggested Approach

1. Build and verify all three application artifacts locally
2. Set up cloud database instances (three databases)
3. Configure database security and credentials
4. Deploy User Service first
5. Test User Service independently
6. Deploy Catalog Service second
7. Test Catalog Service independently
8. Deploy Reservation Service last
9. Configure inter-service communication URLs
10. Test complete workflows across all services
11. Verify health endpoints for all services
12. Test all API functionality end-to-end
13. Document deployment (URLs, credentials, configuration)

**Note:** You have flexibility in choosing cloud platform services and configuration approaches. Focus on achieving working, secure, production deployments that meet all acceptance criteria. Follow `production-enviroment-setup.md` for the tested deployment; separate environments per service exceed the sandbox's instance limit.

---

## Resources

- Refer to `user-stories.md` for all functionality to verify in production
- Refer to `api-contracts.md` for endpoint testing
- Refer to `production-environment-setup.md` for detailed AWS setup
- Refer to `milestone-1` for microservices architecture overview