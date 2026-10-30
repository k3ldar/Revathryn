using System.Security.Cryptography;
using System.Text;

using Revathryn.Contracts;
using Revathryn.Contracts.Models;

namespace Revathryn.Engine
{
    /// <summary>Computes fingerprints for review definitions.</summary>
    public static class ReviewDefinitionVersionCalculator
    {
        /// <summary>Computes a fingerprint of the review-defining configuration.</summary>
        public static string Compute(ReviewType reviewType)
        {
            string value = string.Join(
                Consts.Pipe,
                reviewType.Name,
                reviewType.Description,
                reviewType.FileExtensions,
                reviewType.Prompt);

            byte[] hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));
            return Convert.ToHexString(hashBytes).ToLowerInvariant();
        }
    }
}