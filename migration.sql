CREATE TABLE IF NOT EXISTS "__EFMigrationsHistory" (
    "MigrationId" character varying(150) NOT NULL,
    "ProductVersion" character varying(32) NOT NULL,
    CONSTRAINT "PK___EFMigrationsHistory" PRIMARY KEY ("MigrationId")
);

START TRANSACTION;
CREATE TABLE "Users" (
    "Id" uuid NOT NULL,
    "Email" character varying(255) NOT NULL,
    "PasswordHash" text NOT NULL,
    "FirstName" character varying(100) NOT NULL,
    "LastName" character varying(100) NOT NULL,
    "PhoneNumber" character varying(20) NOT NULL,
    "Role" integer NOT NULL,
    "IsEmailVerified" boolean NOT NULL DEFAULT FALSE,
    "EmailVerificationOTP" character varying(6) NOT NULL,
    "OTPExpiresAt" timestamp with time zone,
    "IsActive" boolean NOT NULL DEFAULT TRUE,
    "CreatedAt" timestamp with time zone NOT NULL DEFAULT (GETUTCDATE()),
    "UpdatedAt" timestamp with time zone NOT NULL DEFAULT (GETUTCDATE()),
    CONSTRAINT "PK_Users" PRIMARY KEY ("Id")
);

CREATE TABLE "Tickets" (
    "Id" uuid NOT NULL,
    "TicketNumber" character varying(20) NOT NULL,
    "Title" character varying(255) NOT NULL,
    "Description" text NOT NULL,
    "CreatedByUserId" uuid NOT NULL,
    "AssignedToUserId" uuid,
    "AssignedByUserId" uuid,
    "ClosedByUserId" uuid,
    "Status" integer NOT NULL,
    "Priority" integer NOT NULL,
    "Category" character varying(100) NOT NULL,
    "CreatedAt" timestamp with time zone NOT NULL DEFAULT (GETUTCDATE()),
    "UpdatedAt" timestamp with time zone NOT NULL DEFAULT (GETUTCDATE()),
    "ClosedAt" timestamp with time zone,
    CONSTRAINT "PK_Tickets" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_Tickets_Users_AssignedByUserId" FOREIGN KEY ("AssignedByUserId") REFERENCES "Users" ("Id") ON DELETE SET NULL,
    CONSTRAINT "FK_Tickets_Users_AssignedToUserId" FOREIGN KEY ("AssignedToUserId") REFERENCES "Users" ("Id") ON DELETE SET NULL,
    CONSTRAINT "FK_Tickets_Users_ClosedByUserId" FOREIGN KEY ("ClosedByUserId") REFERENCES "Users" ("Id") ON DELETE SET NULL,
    CONSTRAINT "FK_Tickets_Users_CreatedByUserId" FOREIGN KEY ("CreatedByUserId") REFERENCES "Users" ("Id") ON DELETE RESTRICT
);

CREATE TABLE "TicketActivityLogs" (
    "Id" uuid NOT NULL,
    "TicketId" uuid NOT NULL,
    "ChangedByUserId" uuid NOT NULL,
    "Action" character varying(100) NOT NULL,
    "OldValue" character varying(255) NOT NULL,
    "NewValue" character varying(255) NOT NULL,
    "ChangedAt" timestamp with time zone NOT NULL DEFAULT (GETUTCDATE()),
    CONSTRAINT "PK_TicketActivityLogs" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_TicketActivityLogs_Tickets_TicketId" FOREIGN KEY ("TicketId") REFERENCES "Tickets" ("Id") ON DELETE CASCADE,
    CONSTRAINT "FK_TicketActivityLogs_Users_ChangedByUserId" FOREIGN KEY ("ChangedByUserId") REFERENCES "Users" ("Id") ON DELETE RESTRICT
);

CREATE TABLE "TicketComments" (
    "Id" uuid NOT NULL,
    "TicketId" uuid NOT NULL,
    "UserId" uuid NOT NULL,
    "Comment" text NOT NULL,
    "AttachmentUrl" character varying(500) NOT NULL,
    "CreatedAt" timestamp with time zone NOT NULL DEFAULT (GETUTCDATE()),
    CONSTRAINT "PK_TicketComments" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_TicketComments_Tickets_TicketId" FOREIGN KEY ("TicketId") REFERENCES "Tickets" ("Id") ON DELETE CASCADE,
    CONSTRAINT "FK_TicketComments_Users_UserId" FOREIGN KEY ("UserId") REFERENCES "Users" ("Id") ON DELETE RESTRICT
);

CREATE INDEX "IX_TicketActivityLogs_ChangedByUserId" ON "TicketActivityLogs" ("ChangedByUserId");

CREATE INDEX "IX_TicketActivityLogs_TicketId" ON "TicketActivityLogs" ("TicketId");

CREATE INDEX "IX_TicketComments_TicketId" ON "TicketComments" ("TicketId");

CREATE INDEX "IX_TicketComments_UserId" ON "TicketComments" ("UserId");

CREATE INDEX "IX_Tickets_AssignedByUserId" ON "Tickets" ("AssignedByUserId");

CREATE INDEX "IX_Tickets_AssignedToUserId" ON "Tickets" ("AssignedToUserId");

CREATE INDEX "IX_Tickets_ClosedByUserId" ON "Tickets" ("ClosedByUserId");

CREATE INDEX "IX_Tickets_CreatedByUserId" ON "Tickets" ("CreatedByUserId");

CREATE UNIQUE INDEX "IX_Tickets_TicketNumber" ON "Tickets" ("TicketNumber");

CREATE UNIQUE INDEX "IX_Users_Email" ON "Users" ("Email");

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20260302122355_InitialCreate', '10.0.3');

COMMIT;

