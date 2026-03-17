# Leave Management System (LMS)

A professional, streamlined web application built with ASP.NET Core 10.0 to manage employee time-off requests, approvals, and leave allocations.

## 🚀 Key Features
- **Role-Based Access**: Specialized portals for Employees, Managers, and Admins.
- **Premium Landing Page**: Professional home page with clear navigation and call-to-actions.
- **Leave Requests**: Apply for leave with support for file attachments (Medical Certificates, etc.).
- **Approval Workflow**: Multi-level approval (Employees apply -> Managers approve).
- **Conflict Detection**: Automated alerts when multiple team members are away on the same dates.
- **Interactive Reports**: Data visualization for team leave trends using Chart.js.
- **Leave Allocation**: Admin tool for bulk or individual leave day management.

---

## 🔐 Default Admin Credentials
To access the system as an administrator:
- **Email**: `admin@lms.com`
- **Password**: `Admin@123`

---

## 🔄 User Flow & Roles

### 1. 👤 Employee Flow
1. **Register/Login**: Create an account and wait for admin activation.
2. **Dashboard**: View current leave balances and request status.
3. **Apply Leave**: Fill out the form, attach documents if needed, and submit.
4. **History**: Track approval status and cancel upcoming leaves if plans change.

### 2. 👥 Manager Flow
1. **My Team**: View direct reports and their availability.
2. **Review Requests**: Approve or reject pending requests from team members with balance warnings.
3. **Team Calendar**: See a visual timeline of team leaves with conflict alerts.
4. **Reports**: View analytics on team leave usage and top consumers.

### 3. 🛡️ Admin Flow
1. **Manage Roles**: Activate new users and assign them to Manager or Employee roles.
2. **Leave Types**: Configure company-wide leave policies (Annual, Sick, Casual).
3. **Leave Allocations**: Bulk assign leave days to all users for the current year.

---

## 🛠️ Technology Stack
- **Backend**: ASP.NET Core 10.0 (MVC)
- **Database**: Entity Framework Core with SQLite
- **Frontend**: Bootstrap 5, FontAwesome, Chart.js, Outfit Web Font
- **Icons**: FontAwesome 6

---

## ⚙️ Getting Started
1. Ensure you have the **.NET 10 SDK** installed.
2. Clone the repository.
3. Run migrations and update the database:
   ```bash
   dotnet ef database update
   ```
4. Start the application:
   ```bash
   dotnet run
   ```
5. Navigate to `http://localhost:5032` (or the port specified in your console).
