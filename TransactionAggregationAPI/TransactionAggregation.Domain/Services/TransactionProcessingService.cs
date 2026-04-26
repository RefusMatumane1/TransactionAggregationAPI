
//using TransactionAggregation.Domain.Common.ValueObjects;
//using TransactionAggregation.Domain.Entities;
//using TransactionAggregation.Domain.Exceptions;

//namespace TransactionAggregation.Domain.Services
//{
//    public class TransactionProcessingService : ITransactionProcessingService
//    {
//        private readonly ITransactionCategorizationStrategy _categorizationStrategy;
//        private readonly ITransactionValidator _validator;

//        public TransactionProcessingService(
//            ITransactionCategorizationStrategy categorizationStrategy,
//            ITransactionValidator validator)
//        {
//            _categorizationStrategy = categorizationStrategy;
//            _validator = validator;
//        }

//        public async Task<Transaction> ProcessTransactionAsync(
//            CustomerId customerId,
//            Money amount,
//            string description,
//            TransactionSource source,
//            DateTime date,
//            CancellationToken cancellationToken = default)
//        {
//            // Validate transaction
//            if (!await _validator.ValidateTransactionAsync(amount, description, source, date, cancellationToken))
//            {
//                throw DomainException.InvalidAmount("Transaction failed validation checks");
//            }

//            // Auto-categorize
//            var category = await _categorizationStrategy.CategorizeAsync(
//                description,
//                amount,
//                source,
//                cancellationToken);

//            // Create transaction
//            var transaction = Transaction.Create(
//                customerId,
//                amount,
//                description,
//                category,
//                source,
//                date);

//            // Auto-approve small transactions
//            if (amount.AbsoluteAmount < 1000)
//            {
//                transaction.Approve("Auto-approval system", "Small transaction auto-approved");
//            }
//            else
//            {
//                transaction.Flag("Large transaction requires review");
//            }

//            return transaction;
//        }

//        public async Task<Transaction> AutoCategorizeTransactionAsync(
//            Transaction transaction,
//            CancellationToken cancellationToken = default)
//        {
//            var newCategory = await _categorizationStrategy.CategorizeAsync(
//                transaction.Description,
//                transaction.Amount,
//                transaction.Source,
//                cancellationToken);

//            if (newCategory != transaction.Category)
//            {
//                transaction.Categorize(newCategory, "Auto-categorized by ML model", true);
//            }

//            return transaction;
//        }

//        public async Task<bool> ValidateTransactionAsync(
//            Transaction transaction,
//            CancellationToken cancellationToken = default)
//        {
//            return await _validator.ValidateTransactionAsync(
//                transaction.Amount,
//                transaction.Description,
//                transaction.Source,
//                transaction.Date,
//                cancellationToken);
//        }
//    }
//}
