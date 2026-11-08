# Contributing to Revathryn

First of all, thank you for considering contributing to Revathryn!

Revathryn is an open-source project built around a simple idea: software quality can be improved through better understanding, intelligent analysis, independent validation and collaboration.

The goal is to build more than an AI code reviewer. Revathryn aims to become an extensible software quality and assurance platform that helps developers understand their repositories, identify potential problems, validate findings and ultimately improve the quality of the software they build.

We believe this is something worth building together.

Whether you're reporting a bug, suggesting an improvement, improving documentation, developing a new review engine or contributing a substantial feature, your involvement is welcome.

## Our ethos

There are a few principles that guide the development of Revathryn.

### Quality over quantity

Finding more issues does not necessarily mean finding more problems.

We want Revathryn to identify meaningful, actionable findings rather than overwhelm developers with noise, speculative observations or false positives.

Contributions should aim to improve the usefulness, accuracy and reliability of the platform.

### Transparency and explainability

Findings should be understandable, traceable and supported by evidence.

We want developers to understand why something has been identified, what context was considered and how conclusions were reached.

Black-box behaviour and unexplained recommendations are not the direction we want to take.

### Independent validation

AI-generated findings should not automatically be treated as facts.

Revathryn is being designed around the principle that findings should be capable of independent review and validation. We welcome contributions that improve verification, confidence assessment and the distinction between suspected and confirmed problems.

### Extensibility and openness

Revathryn should not be tied to one programming language, AI provider, model or particular way of working.

We value contributions that make the platform more adaptable, modular and accessible to different development environments.

### Practical engineering

Revathryn is intended to be used on real software projects.

We value maintainable implementations, sensible abstractions, useful tests, good documentation and solutions that address genuine development problems.

Complexity for its own sake is not a goal.

### Collaboration without ego

Good ideas can come from anywhere.

We welcome constructive disagreement, alternative approaches and thoughtful challenges to existing decisions. The objective is to build something better, not to defend individual implementations or ideas.

## Ways to contribute

There are many ways to help.

**Report bugs**

If something isn't working as expected, please open a GitHub issue. Include steps to reproduce the problem, relevant logs and any useful environmental information.

**Suggest features**

Have an idea that could make Revathryn more useful? Open an issue describing the problem you're trying to solve, why it matters and how you envisage the solution.

We particularly welcome ideas that extend the platform's capabilities beyond its existing roadmap.

**Improve documentation**

Good documentation makes a project accessible. Improvements to setup instructions, explanations, examples, architecture documentation and guides are all valuable contributions.

**Contribute code**

We welcome bug fixes, performance improvements, architectural improvements, new features, integrations and additional review capabilities.

**Develop review capabilities**

One of the most interesting areas of Revathryn is its extensible review engine architecture.

Contributions that introduce new review types, improve existing analysis, integrate specialist tooling or enhance independent validation are particularly welcome.

**Share your experience**

Using Revathryn on your own repositories and sharing what works, what doesn't and what could be better is extremely valuable.

You do not need to contribute code to make a meaningful contribution.

## Before starting work

For substantial changes, please open an issue or start a GitHub Discussion before submitting a pull request.

This helps establish whether the proposed change fits the direction of the project and provides an opportunity to discuss implementation approaches before work begins.

For smaller changes, such as documentation corrections, straightforward bug fixes or minor improvements, feel free to submit a pull request directly.

Please check existing issues and pull requests first to avoid duplicating work.

If you are considering a significant architectural change, we strongly encourage discussing it before implementation.

## Development guidelines

When contributing code, please aim to follow these principles:

- Follow the existing architectural patterns and conventions.
- Keep changes focused and avoid unrelated modifications.
- Prefer readable, maintainable code over clever or unnecessarily complex solutions.
- Include appropriate tests for new functionality and bug fixes.
- Update documentation where behaviour or functionality changes.
- Avoid introducing unnecessary dependencies.
- Consider failure scenarios, logging and operational reliability.
- Do not introduce hard-coded credentials, API keys or other secrets.
- Respect the separation between platform functionality, review engines and AI provider integrations.

Revathryn is built using C# and ASP.NET Core MVC with Razor. Please follow the existing project structure and technology choices unless a change has been discussed and agreed.

## Pull request guidelines

When submitting a pull request:

1. Provide a clear description of the change.
2. Explain the problem being addressed or the capability being introduced.
3. Reference any related GitHub issues.
4. Include relevant tests or explain how the change was verified.
5. Document any configuration changes or additional dependencies.
6. Keep the pull request focused on a single logical change where practical.

Pull requests will be reviewed with consideration for correctness, maintainability, architectural fit, security and consistency with the project's principles.

Feedback and requested changes are part of the collaborative process. Please don't take review comments personally; they are intended to improve the quality of the project.

## AI-assisted contributions

AI-assisted development tools are welcome. We recognise that AI is becoming an increasingly important part of software development, and it would be somewhat ironic for Revathryn to discourage their use!

However, contributors remain responsible for the code they submit.

Please ensure that:

- You understand the code being submitted.
- Generated code has been reviewed rather than accepted blindly.
- Tests have been run where appropriate.
- No proprietary code, confidential information or private repository content has been introduced.
- Any AI-generated contribution complies with the project's licence and applicable third-party licences.

The use of AI should improve the contribution, not replace engineering judgement.

## Licensing

Revathryn is distributed under the GNU Affero General Public License v3.0 (AGPL-3.0).

By submitting a contribution, you agree that your contribution will be made available under the same licence.

Please only contribute code and other materials that you have the right to submit under these terms.

Third-party dependencies and contributions must respect their applicable licensing requirements.

## Security issues

Please do not report exploitable security vulnerabilities through public GitHub issues or discussions.

Instead, follow the instructions in [SECURITY.md](SECURITY.md) to report security concerns responsibly.

We take the security of Revathryn and its users seriously.

## Community expectations

We want Revathryn to be a welcoming and constructive place for developers of all experience levels.

Please:

- Be respectful and considerate.
- Focus discussions on ideas and technical merits.
- Provide constructive feedback.
- Be open to alternative approaches.
- Assume good intentions.
- Help others learn and understand.

Harassment, personal attacks, discriminatory behaviour and deliberately disruptive conduct have no place in the project.

We expect everyone participating in the project to follow these principles.

## A note on direction

Revathryn is an evolving project.

Not every proposed feature will necessarily be accepted, and the roadmap may change as the platform develops and the community provides feedback.

We want to remain open to new ideas without losing sight of the underlying purpose: building a reliable, extensible and genuinely useful software quality platform.

We would rather implement fewer capabilities well than accumulate features that don't deliver meaningful value.

## Thank you

Open-source projects exist because people are willing to share their time, knowledge and experience.

Every contribution matters, whether it is a line of code, a thoughtful issue, a documentation improvement or simply sharing feedback from using the platform.

If you believe in the idea behind Revathryn, we'd love to have you involved.

**Let's build better software, together.**
