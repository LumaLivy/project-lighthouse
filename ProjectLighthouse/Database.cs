using System;
using System.Linq;
using System.Threading.Tasks;
using Kettu;
using LBPUnion.ProjectLighthouse.Helpers;
using LBPUnion.ProjectLighthouse.Logging;
using LBPUnion.ProjectLighthouse.Types;
using LBPUnion.ProjectLighthouse.Types.Levels;
using LBPUnion.ProjectLighthouse.Types.Profiles;
using LBPUnion.ProjectLighthouse.Types.Reviews;
using LBPUnion.ProjectLighthouse.Types.Settings;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace LBPUnion.ProjectLighthouse
{
    public class Database : DbContext
    {
        public DbSet<User> Users { get; set; }
        public DbSet<Location> Locations { get; set; }
        public DbSet<Slot> Slots { get; set; }
        public DbSet<QueuedLevel> QueuedLevels { get; set; }
        public DbSet<HeartedLevel> HeartedLevels { get; set; }
        public DbSet<HeartedProfile> HeartedProfiles { get; set; }
        public DbSet<Comment> Comments { get; set; }
        public DbSet<GameToken> GameTokens { get; set; }
        public DbSet<WebToken> WebTokens { get; set; }
        public DbSet<Score> Scores { get; set; }
        public DbSet<PhotoSubject> PhotoSubjects { get; set; }
        public DbSet<Photo> Photos { get; set; }
        public DbSet<LastContact> LastContacts { get; set; }
        public DbSet<VisitedLevel> VisitedLevels { get; set; }
        public DbSet<RatedLevel> RatedLevels { get; set; }
        public DbSet<AuthenticationAttempt> AuthenticationAttempts { get; set; }
        public DbSet<Review> Reviews { get; set; }
        public DbSet<RatedReview> RatedReviews { get; set; }
        public DbSet<UserApprovedIpAddress> UserApprovedIpAddresses { get; set; }

        protected override void OnConfiguring(DbContextOptionsBuilder options)
            => options.UseMySql(ServerSettings.Instance.DbConnectionString, MySqlServerVersion.LatestSupportedServerVersion);

        public async Task<User> CreateUser(string username, string password)
        {
            if (!password.StartsWith("$")) throw new ArgumentException(nameof(password) + " is not a BCrypt hash");

            User user;
            if ((user = await this.Users.Where(u => u.Username == username).FirstOrDefaultAsync()) != null) return user;

            Location l = new(); // store to get id after submitting
            this.Locations.Add(l); // add to table
            await this.SaveChangesAsync(); // saving to the database returns the id and sets it on this entity

            user = new User
            {
                Username = username,
                Password = password,
                LocationId = l.Id,
                Biography = username + " hasn't introduced themselves yet.",
            };
            this.Users.Add(user);

            await this.SaveChangesAsync();

            return user;
        }

        #nullable enable
        public async Task<GameToken?> AuthenticateUser(LoginData loginData, string userLocation, string titleId = "")
        {
            User? user = await this.Users.FirstOrDefaultAsync(u => u.Username == loginData.Username);
            if (user == null) return null;

            GameToken gameToken = new()
            {
                UserToken = HashHelper.GenerateAuthToken(),
                UserId = user.UserId,
                UserLocation = userLocation,
                GameVersion = GameVersionHelper.FromTitleId(titleId),
            };

            if (gameToken.GameVersion == GameVersion.Unknown)
            {
                Logger.Log($"Unknown GameVersion for TitleId {titleId}", LoggerLevelLogin.Instance);
                return null;
            }

            this.GameTokens.Add(gameToken);
            await this.SaveChangesAsync();

            return gameToken;
        }

        #region Hearts & Queues

        public async Task HeartUser(User user, User heartedUser)
        {
            HeartedProfile? heartedProfile = await this.HeartedProfiles.FirstOrDefaultAsync
                (q => q.UserId == user.UserId && q.HeartedUserId == heartedUser.UserId);
            if (heartedProfile != null) return;

            this.HeartedProfiles.Add
            (
                new HeartedProfile
                {
                    HeartedUserId = heartedUser.UserId,
                    UserId = user.UserId,
                }
            );

            await this.SaveChangesAsync();
        }

        public async Task UnheartUser(User user, User heartedUser)
        {
            HeartedProfile? heartedProfile = await this.HeartedProfiles.FirstOrDefaultAsync
                (q => q.UserId == user.UserId && q.HeartedUserId == heartedUser.UserId);
            if (heartedProfile != null) this.HeartedProfiles.Remove(heartedProfile);

            await this.SaveChangesAsync();
        }

        public async Task HeartLevel(User user, Slot heartedSlot)
        {
            HeartedLevel? heartedLevel = await this.HeartedLevels.FirstOrDefaultAsync(q => q.UserId == user.UserId && q.SlotId == heartedSlot.SlotId);
            if (heartedLevel != null) return;

            this.HeartedLevels.Add
            (
                new HeartedLevel
                {
                    SlotId = heartedSlot.SlotId,
                    UserId = user.UserId,
                }
            );

            await this.SaveChangesAsync();
        }

        public async Task UnheartLevel(User user, Slot heartedSlot)
        {
            HeartedLevel? heartedLevel = await this.HeartedLevels.FirstOrDefaultAsync(q => q.UserId == user.UserId && q.SlotId == heartedSlot.SlotId);
            if (heartedLevel != null) this.HeartedLevels.Remove(heartedLevel);

            await this.SaveChangesAsync();
        }

        public async Task QueueLevel(User user, Slot queuedSlot)
        {
            QueuedLevel? queuedLevel = await this.QueuedLevels.FirstOrDefaultAsync(q => q.UserId == user.UserId && q.SlotId == queuedSlot.SlotId);
            if (queuedLevel != null) return;

            this.QueuedLevels.Add
            (
                new QueuedLevel
                {
                    SlotId = queuedSlot.SlotId,
                    UserId = user.UserId,
                }
            );

            await this.SaveChangesAsync();
        }

        public async Task UnqueueLevel(User user, Slot queuedSlot)
        {
            QueuedLevel? queuedLevel = await this.QueuedLevels.FirstOrDefaultAsync(q => q.UserId == user.UserId && q.SlotId == queuedSlot.SlotId);
            if (queuedLevel != null) this.QueuedLevels.Remove(queuedLevel);

            await this.SaveChangesAsync();
        }

        #endregion

        #region Game Token Shenanigans

        public async Task<User?> UserFromMMAuth(string authToken, bool allowUnapproved = false)
        {
            if (ServerStatics.IsUnitTesting) allowUnapproved = true;
            GameToken? token = await this.GameTokens.FirstOrDefaultAsync(t => t.UserToken == authToken);

            if (token == null) return null;
            if (!allowUnapproved && !token.Approved) return null;

            return await this.Users.Include(u => u.Location).FirstOrDefaultAsync(u => u.UserId == token.UserId);
        }

        public async Task<User?> UserFromGameToken
            (GameToken gameToken, bool allowUnapproved = false)
            => await this.UserFromMMAuth(gameToken.UserToken, allowUnapproved);

        public async Task<User?> UserFromGameRequest(HttpRequest request, bool allowUnapproved = false)
        {
            if (ServerStatics.IsUnitTesting) allowUnapproved = true;
            if (!request.Cookies.TryGetValue("MM_AUTH", out string? mmAuth) || mmAuth == null) return null;

            return await this.UserFromMMAuth(mmAuth, allowUnapproved);
        }

        public async Task<GameToken?> GameTokenFromRequest(HttpRequest request, bool allowUnapproved = false)
        {
            if (ServerStatics.IsUnitTesting) allowUnapproved = true;
            if (!request.Cookies.TryGetValue("MM_AUTH", out string? mmAuth) || mmAuth == null) return null;

            GameToken? token = await this.GameTokens.FirstOrDefaultAsync(t => t.UserToken == mmAuth);

            if (token == null) return null;
            if (!allowUnapproved && !token.Approved) return null;

            return token;
        }

        public async Task<(User, GameToken)?> UserAndGameTokenFromRequest(HttpRequest request, bool allowUnapproved = false)
        {
            if (ServerStatics.IsUnitTesting) allowUnapproved = true;
            if (!request.Cookies.TryGetValue("MM_AUTH", out string? mmAuth) || mmAuth == null) return null;

            GameToken? token = await this.GameTokens.FirstOrDefaultAsync(t => t.UserToken == mmAuth);
            if (token == null) return null;
            if (!allowUnapproved && !token.Approved) return null;

            User? user = await this.UserFromGameToken(token);

            if (user == null) return null;

            return (user, token);
        }

        #endregion

        #region Web Token Shenanigans

        public User? UserFromLighthouseToken(string lighthouseToken)
        {
            WebToken? token = this.WebTokens.FirstOrDefault(t => t.UserToken == lighthouseToken);
            if (token == null) return null;

            return this.Users.Include(u => u.Location).FirstOrDefault(u => u.UserId == token.UserId);
        }

        public User? UserFromWebRequest(HttpRequest request)
        {
            if (!request.Cookies.TryGetValue("LighthouseToken", out string? lighthouseToken) || lighthouseToken == null) return null;

            return this.UserFromLighthouseToken(lighthouseToken);
        }

        public WebToken? WebTokenFromRequest(HttpRequest request)
        {
            if (!request.Cookies.TryGetValue("LighthouseToken", out string? lighthouseToken) || lighthouseToken == null) return null;

            return this.WebTokens.FirstOrDefault(t => t.UserToken == lighthouseToken);
        }

        #endregion

        public async Task<Photo?> PhotoFromSubject(PhotoSubject subject)
            => await this.Photos.FirstOrDefaultAsync(p => p.PhotoSubjectIds.Contains(subject.PhotoSubjectId.ToString()));

        public async Task RemoveUser(User user)
        {
            this.Locations.Remove(user.Location);
            LastContact? lastContact = await this.LastContacts.FirstOrDefaultAsync(l => l.UserId == user.UserId);
            if (lastContact != null) this.LastContacts.Remove(lastContact);

            foreach (Slot slot in this.Slots.Where(s => s.CreatorId == user.UserId)) await this.RemoveSlot(slot, false);

            this.AuthenticationAttempts.RemoveRange(this.AuthenticationAttempts.Include(a => a.GameToken).Where(a => a.GameToken.UserId == user.UserId));
            this.HeartedProfiles.RemoveRange(this.HeartedProfiles.Where(h => h.UserId == user.UserId));
            this.PhotoSubjects.RemoveRange(this.PhotoSubjects.Where(s => s.UserId == user.UserId));
            this.HeartedLevels.RemoveRange(this.HeartedLevels.Where(h => h.UserId == user.UserId));
            this.VisitedLevels.RemoveRange(this.VisitedLevels.Where(v => v.UserId == user.UserId));
            this.QueuedLevels.RemoveRange(this.QueuedLevels.Where(q => q.UserId == user.UserId));
            this.RatedLevels.RemoveRange(this.RatedLevels.Where(r => r.UserId == user.UserId));
            this.GameTokens.RemoveRange(this.GameTokens.Where(t => t.UserId == user.UserId));
            this.WebTokens.RemoveRange(this.WebTokens.Where(t => t.UserId == user.UserId));
            this.Comments.RemoveRange(this.Comments.Where(c => c.PosterUserId == user.UserId));
            this.Photos.RemoveRange(this.Photos.Where(p => p.CreatorId == user.UserId));

            await this.SaveChangesAsync();
        }

        public async Task RemoveSlot(Slot slot, bool saveChanges = true)
        {
            if (slot.Location != null) this.Locations.Remove(slot.Location);
            this.Slots.Remove(slot);

            if (saveChanges) await this.SaveChangesAsync();
        }
        #nullable disable
    }
}