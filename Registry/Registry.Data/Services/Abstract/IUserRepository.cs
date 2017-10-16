﻿using System.Threading.Tasks;
using Registry.Data.Models;

namespace Registry.Data.Services.Abstract
{
  public interface IUserRepository
  {
    Task CreateUser(CreateUserRequest request);

    Task UpdateUser(UpdateUserRequest request);

    Task DeleteUser(string login);

    Task<GetAllUsersResult[]> GetAllUsers();

    Task<GetUserByLoginResult> GetUserByLogin(string login);
  }
}