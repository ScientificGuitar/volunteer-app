import { Link } from "react-router-dom"
import { OPERATOR_NAME } from "./legal"
import { ContactLine, ExternalLink, LegalLayout, LegalSection } from "./LegalLayout"

export function TermsOfServicePage() {
  return (
    <LegalLayout
      title="Terms of Service"
      intro={`${OPERATOR_NAME} is a volunteer-scheduling service: organizations create events with time slots and share an invite link, and volunteers sign up for a slot through a public page — no volunteer account required. These terms apply to both organizers and volunteers who use the service.`}
      sibling={{ to: "/privacy-policy", label: "Privacy Policy" }}
    >
      <LegalSection id="who-we-are" title="1. Who we are">
        <p>
          {OPERATOR_NAME} (“we”, “us”) provides the volunteer-scheduling
          service described above. <ContactLine />
        </p>
      </LegalSection>

      <LegalSection id="who-these-terms-cover" title="2. Who these terms cover">
        <p>
          These terms have two audiences. An <strong>organizer</strong> is
          someone who signs in, creates an organization, and runs events. A{" "}
          <strong>volunteer</strong> is someone who opens an invite link and
          signs up for a slot. Some sections apply to everyone; others are
          marked for organizers or volunteers specifically.
        </p>
        <p>
          By using {OPERATOR_NAME} — creating an account, running an event, or
          submitting a signup — you agree to these terms. If you do not agree,
          please do not use the service.
        </p>
      </LegalSection>

      <LegalSection id="what-we-provide" title="3. What we provide">
        <p>For organizers, {OPERATOR_NAME} lets you:</p>
        <ul className="list-disc space-y-1 pl-6">
          <li>create an organization and events with volunteer time slots,</li>
          <li>share a public invite link with volunteers,</li>
          <li>
            see who has signed up, which slots are full, and where you still
            need people,
          </li>
          <li>manage or remove signups and export the volunteer list.</li>
        </ul>
        <p>For volunteers, {OPERATOR_NAME} lets you:</p>
        <ul className="list-disc space-y-1 pl-6">
          <li>pick a slot and sign up with your name and email address,</li>
          <li>confirm your signup through a link sent to your email,</li>
          <li>
            view or cancel your signup later using the personal manage link in
            your emails.
          </li>
        </ul>
        <p>
          The service is currently offered free of charge. We may introduce
          paid plans or change the available features in the future; if we do,
          we will update these terms and tell you beforehand.
        </p>
      </LegalSection>

      <LegalSection id="organizer-accounts" title="4. Organizer accounts">
        <p>
          Organizers sign in through our authentication provider,{" "}
          <ExternalLink href="https://clerk.com">Clerk</ExternalLink>. You must
          provide accurate account information and keep your login credentials
          secure. You are responsible for everything done under your account,
          so tell us promptly through the contact above if you believe your
          account has been compromised.
        </p>
        <p>
          You must be at least 16 years old (or the age of digital consent in
          your country, if higher) to create an organizer account. Each account
          is personal to you and may not be sold, rented, or shared in a way
          that lets others impersonate you.
        </p>
      </LegalSection>

      <LegalSection
        id="volunteer-signup"
        title="5. Volunteer signup without an account"
      >
        <p>
          Volunteers do not need an account. To sign up you enter your name
          and email address, and we send a confirmation link to that address.
          Your spot is only confirmed once you click the link — this keeps
          mistyped or fake addresses out of organizers’ rosters.
        </p>
        <p>
          Every confirmation, reminder, and cancellation email contains a
          personal manage link for your signup. Treat it like a password:{" "}
          <strong>anyone with the link can view or cancel your signup</strong>.
          If a newer email has been issued (for example after re-sending a
          confirmation or receiving a reminder), only the newest link works.
        </p>
        <p>
          Volunteers must be at least 16 years old to sign up alone. Younger
          volunteers may only sign up with the involvement and consent of a
          parent or guardian.
        </p>
      </LegalSection>

      <LegalSection
        id="organizer-responsibilities"
        title="6. Responsibilities of organizers"
      >
        <ul className="list-disc space-y-1 pl-6">
          <li>
            <strong>Lawful volunteer contact.</strong> Only invite people you
            have a proper reason to contact, and only use volunteer names and
            email addresses for running your events. Do not add addresses to
            marketing lists or share rosters beyond what running the event
            requires.
          </li>
          <li>
            <strong>Accurate events.</strong> Describe your event, location,
            date, and slots truthfully, and keep them up to date. If an event
            is cancelled or changed, tell your volunteers — do not rely on
            them noticing the roster changed.
          </li>
          <li>
            <strong>Data minimization.</strong> The signup form only asks for
            name and email. Do not use event descriptions or slot labels to
            collect extra sensitive information (for example health details
            or government ID numbers).
          </li>
          <li>
            <strong>Respect volunteer choices.</strong> If a volunteer cancels,
            their spot is released. Do not re-add people who cancelled or
            pursue them outside {OPERATOR_NAME} about it.
          </li>
          <li>
            <strong>Exports.</strong> If you export the volunteer list, you
            become responsible for keeping that copy safe and deleting it when
            you no longer need it.
          </li>
        </ul>
      </LegalSection>

      <LegalSection
        id="volunteer-responsibilities"
        title="7. Responsibilities of volunteers"
      >
        <ul className="list-disc space-y-1 pl-6">
          <li>Give your real name and an email address you can access.</li>
          <li>Only sign yourself up, unless someone asked you to sign them up.</li>
          <li>
            Only sign up for shifts you intend to attend. If your plans change,
            cancel through your manage link so the organizer can offer the
            spot to someone else.
          </li>
          <li>
            Do not submit someone else’s personal data, abusive content, or
            deliberately false signups.
          </li>
        </ul>
      </LegalSection>

      <LegalSection id="acceptable-use" title="8. Acceptable use (everyone)">
        <p>You must not:</p>
        <ul className="list-disc space-y-1 pl-6">
          <li>use the service for anything unlawful or fraudulent,</li>
          <li>
            probe, overload, or circumvent the service — including trying to
            take a slot that is already full, guessing manage links, or
            scraping invite pages,
          </li>
          <li>
            send spam, phishing, or harassing messages through or because of
            the service,
          </li>
          <li>
            upload malware or content that infringes someone else’s rights,
          </li>
          <li>
            misrepresent your identity or your relationship with an
            organization.
          </li>
        </ul>
        <p>
          If you run an event on behalf of an organization, you confirm you
          are authorized to do so.
        </p>
      </LegalSection>

      <LegalSection id="emails" title="9. Email communications">
        <p>
          Transactional emails (signup confirmations, reminders sent around 24
          hours before a shift, and cancellation notices) are an essential
          part of the service and are sent by our email provider,{" "}
          <ExternalLink href="https://resend.com">Resend</ExternalLink>, on
          our behalf. By signing up as a volunteer or running events as an
          organizer, you agree to receive these service emails. We do not send
          marketing emails to volunteers.
        </p>
      </LegalSection>

      <LegalSection id="availability" title="10. Availability and changes">
        <p>
          We work to keep {OPERATOR_NAME} reliable — for example by preventing
          two volunteers from taking the last spot at the same time — but we
          do not guarantee uninterrupted or error-free operation. We may
          modify, suspend, or discontinue parts of the service (for
          maintenance, security, or legal reasons) and will try to give
          reasonable notice when the impact is significant.
        </p>
      </LegalSection>

      <LegalSection id="termination" title="11. Suspension and termination">
        <p>
          Organizers can stop using the service at any time by deleting their
          events and organization or by asking us to delete their account.
          Volunteers can cancel individual signups through their manage link
          at any time.
        </p>
        <p>
          We may suspend or terminate access — for example an organizer
          account or a specific event link — if these terms are violated, if
          required by law, or to protect volunteers, organizers, or the
          service. Where practical we will explain what happened and how to
          appeal.
        </p>
      </LegalSection>

      <LegalSection id="liability" title="12. Disclaimer and liability">
        <p>
          The service is provided “as is” without warranties of any kind,
          except where the law does not allow such exclusions. {OPERATOR_NAME}{" "}
          coordinates signups; it does not employ, insure, or supervise
          volunteers, and organizers are solely responsible for running safe
          events.
        </p>
        <p>
          To the maximum extent permitted by law, our liability for any claim
          connected to the service is limited to the amounts you paid us in
          the 12 months before the claim arose (which is zero while the
          service is free). Nothing in these terms limits liability where the
          law does not permit it — including for death or personal injury
          caused by negligence, fraud, or your statutory consumer and data
          protection rights.
        </p>
      </LegalSection>

      <LegalSection id="changes" title="13. Changes to these terms">
        <p>
          If we change these terms materially, we will post the updated
          version on this page with a new “last updated” date and, where the
          change is significant, give additional notice (for example in the
          app). Continuing to use {OPERATOR_NAME} after the changes take
          effect means you accept them.
        </p>
      </LegalSection>

      <LegalSection id="general" title="14. General">
        <p>
          If any part of these terms is found unenforceable, the rest still
          applies. Our failure to enforce a provision is not a waiver of our
          right to do so later. These terms do not limit any rights you have
          under applicable consumer or data protection law. How we handle
          personal data is described in our{" "}
          <Link
            to="/privacy-policy"
            className="font-medium text-primary underline underline-offset-4"
          >
            Privacy Policy
          </Link>
          , which forms part of these terms.
        </p>
      </LegalSection>
    </LegalLayout>
  )
}
