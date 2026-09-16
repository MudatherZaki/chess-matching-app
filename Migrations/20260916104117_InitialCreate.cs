using System;
using Microsoft.EntityFrameworkCore.Migrations;
using NetTopologySuite.Geometries;

#nullable disable

namespace ChessApp.Backend.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:PostgresExtension:postgis", ",,");

            migrationBuilder.CreateTable(
                name: "users",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    email = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    username = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    password_hash = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    full_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    photo_url = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    bio = table.Column<string>(type: "text", nullable: true),
                    location = table.Column<Point>(type: "geography (point, 4326)", nullable: true),
                    last_location_update = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    is_available = table.Column<bool>(type: "boolean", nullable: false),
                    availability_expires_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    has_board = table.Column<bool>(type: "boolean", nullable: false),
                    fide_id = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    fide_rating = table.Column<int>(type: "integer", nullable: true),
                    fide_rating_updated_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    chess_com_username = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    chess_com_url = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    chess_com_rating = table.Column<int>(type: "integer", nullable: true),
                    lichess_username = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    lichess_url = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    lichess_rating = table.Column<int>(type: "integer", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: false, defaultValueSql: "NOW()"),
                    updated_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: false, defaultValueSql: "NOW()"),
                    last_login = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    device_tokens = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_users", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "blocks",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    blocker_id = table.Column<Guid>(type: "uuid", nullable: false),
                    blocked_id = table.Column<Guid>(type: "uuid", nullable: false),
                    reason = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_blocks", x => x.id);
                    table.CheckConstraint("ck_different_users", "blocker_id != blocked_id");
                    table.ForeignKey(
                        name: "fk_blocks_users_blocked_id",
                        column: x => x.blocked_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_blocks_users_blocker_id",
                        column: x => x.blocker_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "matches",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    player1id = table.Column<Guid>(type: "uuid", nullable: false),
                    player2id = table.Column<Guid>(type: "uuid", nullable: false),
                    proposal_id = table.Column<Guid>(type: "uuid", nullable: true),
                    played_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    location = table.Column<Point>(type: "geography (point, 4326)", nullable: true),
                    outcome = table.Column<string>(type: "text", nullable: false),
                    played_with_board = table.Column<bool>(type: "boolean", nullable: true),
                    time_control = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    pgn = table.Column<string>(type: "text", nullable: true),
                    notes = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: false, defaultValueSql: "NOW()"),
                    updated_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_matches", x => x.id);
                    table.ForeignKey(
                        name: "fk_matches_users_player1id",
                        column: x => x.player1id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_matches_users_player2id",
                        column: x => x.player2id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "rating_snapshots",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    platform = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    rating = table.Column<int>(type: "integer", nullable: false),
                    captured_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_rating_snapshots", x => x.id);
                    table.ForeignKey(
                        name: "fk_rating_snapshots_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "refresh_tokens",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    token_hash = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    expires_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    revoked_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_refresh_tokens", x => x.id);
                    table.ForeignKey(
                        name: "fk_refresh_tokens_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "user_stats",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    total_matches_played = table.Column<int>(type: "integer", nullable: false),
                    total_wins = table.Column<int>(type: "integer", nullable: false),
                    total_losses = table.Column<int>(type: "integer", nullable: false),
                    total_draws = table.Column<int>(type: "integer", nullable: false),
                    avg_opponent_rating = table.Column<int>(type: "integer", nullable: true),
                    longest_streak = table.Column<int>(type: "integer", nullable: false),
                    last_stats_update = table.Column<DateTime>(type: "timestamp without time zone", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_user_stats", x => x.id);
                    table.ForeignKey(
                        name: "fk_user_stats_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "proposals",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    proposer_id = table.Column<Guid>(type: "uuid", nullable: false),
                    receiver_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false),
                    responded_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    expires_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    message = table.Column<string>(type: "text", nullable: true),
                    meeting_location = table.Column<Point>(type: "geography (point, 4326)", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: false, defaultValueSql: "NOW()"),
                    match_id = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_proposals", x => x.id);
                    table.CheckConstraint("ck_different_users", "proposer_id != receiver_id");
                    table.ForeignKey(
                        name: "fk_proposals_matches_match_id",
                        column: x => x.match_id,
                        principalTable: "matches",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_proposals_users_proposer_id",
                        column: x => x.proposer_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_proposals_users_receiver_id",
                        column: x => x.receiver_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_blocks_blocked_id",
                table: "blocks",
                column: "blocked_id");

            migrationBuilder.CreateIndex(
                name: "ix_blocks_blocker_id",
                table: "blocks",
                column: "blocker_id");

            migrationBuilder.CreateIndex(
                name: "ix_blocks_blocker_id_blocked_id",
                table: "blocks",
                columns: new[] { "blocker_id", "blocked_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_matches_played_at",
                table: "matches",
                column: "played_at");

            migrationBuilder.CreateIndex(
                name: "ix_matches_player1id",
                table: "matches",
                column: "player1id");

            migrationBuilder.CreateIndex(
                name: "ix_matches_player2id",
                table: "matches",
                column: "player2id");

            migrationBuilder.CreateIndex(
                name: "ix_proposals_expires_at",
                table: "proposals",
                column: "expires_at");

            migrationBuilder.CreateIndex(
                name: "ix_proposals_match_id",
                table: "proposals",
                column: "match_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_proposals_meeting_location",
                table: "proposals",
                column: "meeting_location")
                .Annotation("Npgsql:IndexMethod", "GIST");

            migrationBuilder.CreateIndex(
                name: "ix_proposals_proposer_id",
                table: "proposals",
                column: "proposer_id");

            migrationBuilder.CreateIndex(
                name: "ix_proposals_receiver_id_status",
                table: "proposals",
                columns: new[] { "receiver_id", "status" },
                filter: "status = 'pending'");

            migrationBuilder.CreateIndex(
                name: "ix_proposals_status",
                table: "proposals",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "ix_rating_snapshots_user_id_platform",
                table: "rating_snapshots",
                columns: new[] { "user_id", "platform" });

            migrationBuilder.CreateIndex(
                name: "ix_refresh_tokens_expires_at",
                table: "refresh_tokens",
                column: "expires_at");

            migrationBuilder.CreateIndex(
                name: "ix_refresh_tokens_token_hash",
                table: "refresh_tokens",
                column: "token_hash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_refresh_tokens_user_id",
                table: "refresh_tokens",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_user_stats_user_id",
                table: "user_stats",
                column: "user_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_users_availability_expires_at",
                table: "users",
                column: "availability_expires_at",
                filter: "is_available = true");

            migrationBuilder.CreateIndex(
                name: "ix_users_email",
                table: "users",
                column: "email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_users_is_available",
                table: "users",
                column: "is_available",
                filter: "is_available = true");

            migrationBuilder.CreateIndex(
                name: "ix_users_location",
                table: "users",
                column: "location")
                .Annotation("Npgsql:IndexMethod", "GIST");

            migrationBuilder.CreateIndex(
                name: "ix_users_username",
                table: "users",
                column: "username",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "blocks");

            migrationBuilder.DropTable(
                name: "proposals");

            migrationBuilder.DropTable(
                name: "rating_snapshots");

            migrationBuilder.DropTable(
                name: "refresh_tokens");

            migrationBuilder.DropTable(
                name: "user_stats");

            migrationBuilder.DropTable(
                name: "matches");

            migrationBuilder.DropTable(
                name: "users");
        }
    }
}
