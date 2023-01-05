using System;
using System.Collections.Generic;
using System.Linq;

namespace maskx.OData.Infrastructure
{
    public class ForeignKey : BaseProperty
    {
        private readonly CSharpUtilities _cSharpUtilities = new CSharpUtilities();
        public ForeignKey(string originalName) : base(originalName) { }
        public ForeignKey(string name, string originalName) : base(name, originalName) { }
        public ForeignKey(string originalName, Entity principal, Entity declaring, List<Property> principalProperties, List<Property> declaringProperties) : base(originalName)
        {
            this.PrincipalEntityType = principal;
            this.DeclaringEntityType = declaring;
            this.PrincipalProperties = principalProperties;
            this.DeclaringProperties = declaringProperties;
            principal.Properties.AddForeignKey(this);
            BuildPrincipalName();
            BuildDeclaringName();
        }

        public Entity PrincipalEntityType { get; private set; }
        public Entity DeclaringEntityType { get; set; }
        public List<Property> PrincipalProperties { get; private set; }
        public List<Property> DeclaringProperties { get; private set; }
        public string PrincipalName { get; private set; }
        public string DeclaringName { get; private set; }
        
        internal void BuildPrincipalName()
        {
            var principalNam = CandidateNamingService.GetPrincipalEndCandidateNavigationPropertyName(this, this.DeclaringName);
            _cSharpUtilities.GenerateCSharpIdentifier(
                principalNam,
                ExistingIdentifiers(this.PrincipalEntityType),
                singularizePluralizer: null);
            this.PrincipalName = principalNam;
        }
        internal void BuildDeclaringName()
        {
            var dependentEndExistingIdentifiers = ExistingIdentifiers(this.DeclaringEntityType);
            var _DeclaringName = CandidateNamingService.GetDependentEndCandidateNavigationPropertyName(this);
            _DeclaringName =
        _cSharpUtilities.GenerateCSharpIdentifier(
            _DeclaringName,
            dependentEndExistingIdentifiers,
            singularizePluralizer: null);
            base.Name = CandidateNamingService.GetPrincipalEndCandidateNavigationPropertyName(this, _DeclaringName);
        }
        protected virtual List<string> ExistingIdentifiers(Entity entityType)
        {
            var existingIdentifiers = new List<string> { entityType.Name };
            existingIdentifiers.AddRange(entityType.Properties.ToList().Select(p => p.Name));
            return existingIdentifiers;
        }
    }
}
