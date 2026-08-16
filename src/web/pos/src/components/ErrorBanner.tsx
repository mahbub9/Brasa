import { useTranslation } from 'react-i18next';
import { Icon } from '@brasa/ui/components/Icon';

interface ErrorBannerProps {
  message: string;
  onDismiss: () => void;
}

export function ErrorBanner({ message, onDismiss }: ErrorBannerProps) {
  const { t } = useTranslation();

  return (
    <div className="error-banner" role="alert">
      <span>{message}</span>
      <button type="button" className="error-banner-dismiss" onClick={onDismiss} aria-label={t('error.dismiss')}>
        <Icon name="close" />
      </button>
    </div>
  );
}
