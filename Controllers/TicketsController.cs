using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using TitanTracker.Data;
using TitanTracker.Extensions;
using TitanTracker.Models;
using TitanTracker.Models.Enums;
using TitanTracker.Models.ViewModels;
using TitanTracker.Services.Interfaces;

namespace TitanTracker.Controllers
{
    public class TicketsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IBTProjectService _projectService;
        private readonly UserManager<BTUser> _userManager;
        private readonly IBTTicketService _ticketService;
        private readonly IBTTicketHistoryService _historyService;
        private readonly IBTNotificationService _notificationService;
        private readonly IBTFileService _fileService;

        public TicketsController(ApplicationDbContext context,
                                 IBTProjectService projectService,
                                 UserManager<BTUser> userManager,
                                 IBTTicketService ticketService,
                                 IBTTicketHistoryService historyService,
                                 IBTNotificationService notificationService,
                                 IBTFileService fileService)
        {
            _context = context;
            _projectService = projectService;
            _userManager = userManager;
            _ticketService = ticketService;
            _historyService = historyService;
            _notificationService = notificationService;
            _fileService = fileService;
        }

        // GET: Tickets
        public async Task<IActionResult> Index()
        {
            var applicationDbContext = _context.Tickets.Include(t => t.DeveloperUser).Include(t => t.OwnerUser).Include(t => t.Project).Include(t => t.TicketPriority).Include(t => t.TicketStatus).Include(t => t.TicketType);
            return View(await applicationDbContext.ToListAsync());
        }

        public async Task<IActionResult> MyTickets()
        {
            int companyId = User.Identity.GetCompanyId().Value;
            string userId = _userManager.GetUserId(User);

            List<Ticket> tickets = await _ticketService.GetTicketsByUserIdAsync(userId, companyId);
            return View(tickets);
        }

        public async Task<IActionResult> AllTickets()
        {
            int companyId = User.Identity.GetCompanyId().Value;

            List<Ticket> tickets = await _ticketService.GetAllTicketsByCompanyAsync(companyId);
            return View(tickets);
        }

        // GET: Tickets/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            //TODO: Ask about usage of "t" vs. "m" below. Should these be uniform?
            //Compare to Bonus Step 2 image found at https://coderfoundry.bit.ai/docs/view/CLRO2d5NGml6TCMy

            var ticket = await _context.Tickets
                .Include(t => t.DeveloperUser)
                .Include(t => t.OwnerUser)
                .Include(t => t.Project)
                .Include(t => t.TicketPriority)
                .Include(t => t.TicketStatus)
                .Include(t => t.TicketType)
                .Include(t => t.Comments)
                .Include(t => t.Attachments)
                .FirstOrDefaultAsync(m => m.Id == id);
            if (ticket == null)
            {
                return NotFound();
            }

            return View(ticket);
        }

        // GET: Tickets/Create
        public async Task<IActionResult> Create()
        {
            // get current user
            BTUser btUser = await _userManager.GetUserAsync(User);

            // get current user's company Id
            int companyId = User.Identity.GetCompanyId().Value;


            if (User.IsInRole(Roles.Admin.ToString()))
            {
                ViewData["ProjectId"] = new SelectList(await _projectService.GetAllProjectsByCompany(companyId), "Id", "Name");
            }
            else
            {
                ViewData["ProjectId"] = new SelectList(await _projectService.GetUserProjectsAsync(btUser.Id), "Id", "Name");
            }


            ViewData["TicketPriorityId"] = new SelectList(_context.TicketPriorities, "Id", "Name");
            ViewData["TicketTypeId"] = new SelectList(_context.TicketTypes, "Id", "Name");

            return View();
        }

        // POST: Tickets/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Id,Title,Description,ProjectId,TicketTypeId,TicketPriorityId")] Ticket ticket)
        {
            if (ModelState.IsValid)
            {
                BTUser btUser = await _userManager.GetUserAsync(User);

                ticket.Created = DateTimeOffset.Now;
                ticket.OwnerUserId = btUser.Id;

                ticket.TicketStatusId = (await _ticketService.LookupTicketStatusIdAsync(BTTicketStatus.New.ToString())).Value;

                await _ticketService.AddNewTicketAsync(ticket);

                #region Ticket History
                //TODO: Add to History -- DONE
                Ticket newTicket = await _ticketService.GetTicketAsNoTrackingAsync(ticket.Id);
                await _historyService.AddHistoryAsync(null, newTicket, btUser.Id);
                #endregion

                #region Ticket Notification
                //TODO: Send Notification
                BTUser projectManager = await _projectService.GetProjectManagerAsync(ticket.ProjectId);
                int companyId = User.Identity.GetCompanyId().Value;

                Notification notification = new()
                {
                    TicketId = ticket.Id,
                    Title = "New Ticket",
                    Message = $"New Ticket: {ticket.Title} was created by {btUser.FullName}",
                    Created = DateTimeOffset.Now,
                    SenderId = btUser.Id,
                    RecipientId = projectManager?.Id
                };

                if (projectManager != null)
                {
                    await _notificationService.AddNotificationAsync(notification);
                    await _notificationService.SendEmailNotificationAsync(notification, "New Ticket Added");
                }
                else
                {
                    //Admin notification
                    await _notificationService.AddNotificationAsync(notification);
                    await _notificationService.SendEmailNotificationsByRoleAsync(notification, companyId, Roles.Admin.ToString());
                }
                #endregion


                return RedirectToAction("Details", "Projects", new { id = ticket.ProjectId });
            }


            return View(ticket);
        }

        // GET: Tickets/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var ticket = await _ticketService.GetTicketByIdAsync(id.Value);

            if (ticket == null)
            {
                return NotFound();
            }

            ViewData["TicketPriorityId"] = new SelectList(_context.TicketPriorities, "Id", "Id", ticket.TicketPriorityId);
            ViewData["TicketStatusId"] = new SelectList(_context.TicketStatuses, "Id", "Id", ticket.TicketStatusId);
            ViewData["TicketTypeId"] = new SelectList(_context.TicketTypes, "Id", "Id", ticket.TicketTypeId);
            return View(ticket);
        }

        // POST: Tickets/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("Id,Title,ProjectId,Description,Created,TicketTypeId,TicketPriorityId,TicketStatusId,OwnerUserId,DeveloperUserId")] Ticket ticket)
        {
            if (id != ticket.Id)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                BTUser btUser = await _userManager.GetUserAsync(User);
                Ticket oldTicket = await _ticketService.GetTicketAsNoTrackingAsync(ticket.Id);
                try
                {
                    ticket.Updated = DateTimeOffset.Now;
                    await _ticketService.UpdateTicketAsync(ticket);

                    //TODO: Send notification

                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!await TicketExistsAsync(ticket.Id))
                    {
                        return NotFound();
                    }
                    else
                    {
                        throw;
                    }
                }

                //TODO: Add History -- DONE
                Ticket newTicket = await _ticketService.GetTicketAsNoTrackingAsync(ticket.Id);

                await _historyService.AddHistoryAsync(oldTicket, newTicket, btUser.Id);


                return RedirectToAction("AllTickets");
            }
            //ViewData["DeveloperUserId"] = new SelectList(_context.Users, "Id", "Id", ticket.DeveloperUserId);
            //ViewData["OwnerUserId"] = new SelectList(_context.Users, "Id", "Id", ticket.OwnerUserId);
            //ViewData["ProjectId"] = new SelectList(_context.Projects, "Id", "Name", ticket.ProjectId);
            //ViewData["TicketPriorityId"] = new SelectList(_context.TicketPriorities, "Id", "Id", ticket.TicketPriorityId);
            //ViewData["TicketStatusId"] = new SelectList(_context.TicketStatuses, "Id", "Id", ticket.TicketStatusId);
            //ViewData["TicketTypeId"] = new SelectList(_context.TicketTypes, "Id", "Id", ticket.TicketTypeId);
            return View(ticket);
        }

        public async Task<IActionResult> AssignDeveloper(int id)
        {
            AssignDeveloperViewModel model = new();

            model.Ticket = await _ticketService.GetTicketByIdAsync(id);

            model.Developers = new SelectList(await _projectService.GetProjectMembersByRoleAsync(model.Ticket.ProjectId, Roles.Developer.ToString()),
                                              "Id", "Full Name");

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AssignDeveloper(AssignDeveloperViewModel model)
        {
            //TODO:
            //Add history
            //Send notifications

            if (model.DeveloperId != null)
            {
                await _ticketService.AssignTicketAsync(model.Ticket.Id, model.DeveloperId);
            }

            return RedirectToAction("All Tickets");
        }
            
        // GET: Tickets/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var ticket = await _context.Tickets
                .Include(t => t.DeveloperUser)
                .Include(t => t.OwnerUser)
                .Include(t => t.Project)
                .Include(t => t.TicketPriority)
                .Include(t => t.TicketStatus)
                .Include(t => t.TicketType)
                .FirstOrDefaultAsync(m => m.Id == id);
            if (ticket == null)
            {
                return NotFound();
            }

            return View(ticket);
        }

        // POST: Tickets/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var ticket = await _context.Tickets.FindAsync(id);
            _context.Tickets.Remove(ticket);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        private async Task<bool> TicketExistsAsync(int id)
        {
            int companyId = User.Identity.GetCompanyId().Value;
            return (await _ticketService.GetAllTicketsByCompanyAsync(companyId)).Any(t => t.Id == id);
        }
    }
}
